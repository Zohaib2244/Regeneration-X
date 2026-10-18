using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Turns any mesh into a cube formation of an exact cube cost.
///
/// The mesh is voxelized on a grid whose spacing is chosen so the shape needs as close to (but not more
/// than) the requested number of cubes. The result is scaled so one voxel = one VOB, which means the
/// cubes tile perfectly with no gaps or overlaps, whatever the source mesh's size.
/// </summary>
public static class FormationVoxelizer
{
    public class Result
    {
        public Vector3[] slots;
        public Vector3[] reserve;
        public int resolution;
        public int surfaceCount;
        public int interiorCount;
        public int duplicateCount;
        public string error;
    }

    const int MinResolution = 1;
    const int MaxResolution = 256;

    // ------------------------------------------------------------------ public entry points

    /// <summary>Bakes <paramref name="source"/> (a GameObject/prefab/model or a Mesh) into slots.</summary>
    public static Result Bake(Object source, int cubeCost, float cubeSize, VOBFormation.FillMode fill, VOBFormation.PivotMode pivot)
    {
        if (!GatherTriangles(source, out var verts, out var tris, out string error))
            return new Result { error = error };
        return Bake(verts, tris, cubeCost, cubeSize, fill, pivot);
    }

    public static Result Bake(List<Vector3> verts, List<int> tris, int cubeCost, float cubeSize,
        VOBFormation.FillMode fill, VOBFormation.PivotMode pivot)
    {
        cubeCost = Mathf.Max(1, cubeCost);
        cubeSize = Mathf.Max(1e-4f, cubeSize);
        if (verts.Count == 0 || tris.Count < 3)
            return new Result { error = "Mesh has no triangles." };

        // Pick the grid resolution.
        //  Hollow: finest grid whose surface shell still fits in the cost (max detail; leftover budget fills just under the surface).
        //  Solid:  coarsest grid whose whole volume covers the cost (every cube is part of the solid).
        int resolution;
        if (fill == VOBFormation.FillMode.Hollow)
            resolution = BestHollowResolution(verts, tris, cubeCost);
        else
        {
            resolution = SmallestResolution(verts, tris, r =>
            {
                var g = Voxelize(verts, tris, r);
                return g.SolidCount >= cubeCost;
            });
            // Thin shapes may never get enough volume without the shell itself overflowing the cost.
            if (Voxelize(verts, tris, resolution).ShellCount > cubeCost)
                resolution = BestHollowResolution(verts, tris, cubeCost);
        }

        var grid = Voxelize(verts, tris, resolution);
        var result = BuildResult(grid, cubeCost, cubeSize, pivot, resolution);
        ClearCache();
        return result;
    }

    // ------------------------------------------------------------------ mesh gathering

    /// <summary>Collects every triangle under a GameObject (MeshFilters and SkinnedMeshRenderers, in their
    /// current pose) or a single Mesh, in the object's own space with its root rotation and scale kept.</summary>
    public static bool GatherTriangles(Object source, out List<Vector3> verts, out List<int> tris, out string error)
    {
        verts = new List<Vector3>();
        tris = new List<int>();
        error = null;

        if (source is Mesh mesh)
        {
            if (!ReadMesh(mesh, out var v, out var t, out error)) return false;
            verts.AddRange(v);
            tris.AddRange(t);
            return true;
        }

        if (!(source is GameObject go))
        {
            error = "Drop a model, prefab, scene object or Mesh.";
            return false;
        }

        // Work on a temporary copy at the origin so prefabs and scene objects behave the same.
        var instance = Object.Instantiate(go);
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.position = Vector3.zero;
        var tempMeshes = new List<Mesh>();
        try
        {
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (!ReadMesh(mf.sharedMesh, out var v, out var t, out error)) return false;
                Append(verts, tris, v, t, mf.transform.localToWorldMatrix);
            }
            foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                var baked = new Mesh();
                tempMeshes.Add(baked);
                // Unscaled, in the renderer's space; localToWorldMatrix below then applies scale exactly once.
                smr.BakeMesh(baked, false);
                Append(verts, tris, baked.vertices, baked.triangles, smr.transform.localToWorldMatrix);
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
            foreach (var m in tempMeshes) Object.DestroyImmediate(m);
        }

        if (tris.Count == 0)
        {
            error = $"'{source.name}' has no MeshFilter or SkinnedMeshRenderer with a mesh.";
            return false;
        }
        return true;
    }

    static void Append(List<Vector3> verts, List<int> tris, Vector3[] v, int[] t, Matrix4x4 m)
    {
        int offset = verts.Count;
        for (int i = 0; i < v.Length; i++) verts.Add(m.MultiplyPoint3x4(v[i]));
        for (int i = 0; i < t.Length; i++) tris.Add(t[i] + offset);
    }

    // Meshes imported without Read/Write can't always be read by scripts; flip it on for the read if needed.
    static bool ReadMesh(Mesh mesh, out Vector3[] verts, out int[] tris, out string error)
    {
        error = null;
        ModelImporter importer = null;
        bool restore = false;
        if (!mesh.isReadable)
        {
            importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                restore = true;
            }
        }
        try
        {
            verts = mesh.vertices;
            tris = mesh.triangles;
        }
        finally
        {
            if (restore)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
        if (verts == null || verts.Length == 0)
        {
            error = $"Couldn't read mesh '{mesh.name}'. Enable Read/Write in its import settings and try again.";
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ resolution search

    // Largest r with ok(r) true, where ok is (roughly) true for small r and false for large r.
    static int LargestResolution(List<Vector3> verts, List<int> tris, Func<int, bool> ok)
    {
        if (!ok(MinResolution)) return MinResolution;
        // Double until it fails, then binary search the last gap.
        int lo = MinResolution, hi = MinResolution * 2;
        while (hi <= MaxResolution && ok(hi)) { lo = hi; hi *= 2; }
        if (hi > MaxResolution) return lo;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (ok(mid)) lo = mid; else hi = mid;
        }
        // Counts aren't perfectly monotonic; peek a little higher.
        for (int r = lo + 1; r <= Mathf.Min(MaxResolution, lo + 3); r++)
            if (ok(r)) lo = r;
        return lo;
    }

    // Finest grid whose outside fits the cost. Thin parts (blades, fins) make counts jump around as the
    // grid gets finer, so after the search, scan nearby grids and keep the one that places the most
    // distinct cubes (fewest stacked duplicates), preferring finer grids on ties.
    static int BestHollowResolution(List<Vector3> verts, List<int> tris, int cost)
    {
        int found = LargestResolution(verts, tris, r => Voxelize(verts, tris, r).ShellCount <= cost);
        int best = found, bestScore = -1;
        for (int r = Mathf.Max(MinResolution, found - 8); r <= Mathf.Min(MaxResolution, found + 24); r++)
        {
            var g = Voxelize(verts, tris, r);
            if (g.ShellCount > cost) continue;
            int score = Mathf.Min(g.SolidCount, cost);
            if (score >= bestScore) { best = r; bestScore = score; }
        }
        return best;
    }

    // Smallest r with ok(r) true, where ok is (roughly) false for small r and true for large r.
    static int SmallestResolution(List<Vector3> verts, List<int> tris, Func<int, bool> ok)
    {
        if (ok(MinResolution)) return MinResolution;
        int lo = MinResolution, hi = MinResolution * 2;
        while (hi <= MaxResolution && !ok(hi)) { lo = hi; hi *= 2; }
        if (hi > MaxResolution) return lo;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (ok(mid)) hi = mid; else lo = mid;
        }
        return hi;
    }

    // ------------------------------------------------------------------ voxelization

    class Grid
    {
        public int nx, ny, nz;
        public float h;
        public Vector3 origin;       // world position of voxel (0,0,0)'s min corner
        public bool[] solid;
        public bool[] shell;
        public int SolidCount, ShellCount;

        public int Index(int x, int y, int z) => x + nx * (y + ny * z);
        public bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < nx && y < ny && z < nz;
        public Vector3 Center(int x, int y, int z) => origin + new Vector3((x + 0.5f) * h, (y + 0.5f) * h, (z + 0.5f) * h);
    }

    static readonly Dictionary<int, Grid> cache = new Dictionary<int, Grid>();
    static List<Vector3> cacheVerts;
    static List<int> cacheTris;

    static void ClearCache()
    {
        cache.Clear();
        cacheVerts = null;
        cacheTris = null;
    }

    /// <param name="resolution">Voxels along the mesh's longest axis.</param>
    static Grid Voxelize(List<Vector3> verts, List<int> tris, int resolution)
    {
        // The resolution search voxelizes the same mesh several times; reuse results.
        if (!ReferenceEquals(verts, cacheVerts) || !ReferenceEquals(tris, cacheTris))
        {
            cache.Clear();
            cacheVerts = verts;
            cacheTris = tris;
        }
        if (cache.TryGetValue(resolution, out var cached)) return cached;

        var bounds = new Bounds(verts[0], Vector3.zero);
        foreach (var v in verts) bounds.Encapsulate(v);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest <= 0f) longest = 1f;

        var g = new Grid();
        g.h = longest / resolution;
        g.nx = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / g.h - 1e-4f));
        g.ny = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / g.h - 1e-4f));
        g.nz = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / g.h - 1e-4f));
        g.origin = bounds.center - new Vector3(g.nx, g.ny, g.nz) * (g.h * 0.5f);
        g.solid = new bool[g.nx * g.ny * g.nz];

        MarkSurface(g, verts, tris);
        FillInterior(g, verts, tris);
        ComputeShell(g);

        cache[resolution] = g;
        return g;
    }

    // Every voxel a triangle passes through (sampled densely enough that thin parts like blades survive).
    static void MarkSurface(Grid g, List<Vector3> verts, List<int> tris)
    {
        float step = g.h * 0.5f;
        for (int t = 0; t + 2 < tris.Count; t += 3)
        {
            Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
            float longestEdge = Mathf.Max((b - a).magnitude, Mathf.Max((c - a).magnitude, (c - b).magnitude));
            int n = Mathf.Clamp(Mathf.CeilToInt(longestEdge / step), 1, 4096);
            for (int i = 0; i <= n; i++)
            for (int j = 0; j <= n - i; j++)
            {
                Vector3 p = a + (b - a) * ((float)i / n) + (c - a) * ((float)j / n);
                Mark(g, p);
            }
        }
    }

    static void Mark(Grid g, Vector3 p)
    {
        Vector3 local = (p - g.origin) / g.h;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x), 0, g.nx - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(local.y), 0, g.ny - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(local.z), 0, g.nz - 1);
        g.solid[g.Index(x, y, z)] = true;
    }

    // Inside test by casting a vertical ray through each column and pairing up the hits.
    // Columns with an odd number of hits (holes in the mesh) are skipped, so open meshes degrade to surface-only.
    static void FillInterior(Grid g, List<Vector3> verts, List<int> tris)
    {
        // Bucket triangles by the columns their XZ footprint covers.
        var buckets = new List<int>[g.nx * g.nz];
        for (int t = 0; t + 2 < tris.Count; t += 3)
        {
            Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - g.origin.x) / g.h - 0.5f), 0, g.nx - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - g.origin.x) / g.h - 0.5f), 0, g.nx - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - g.origin.z) / g.h - 0.5f), 0, g.nz - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - g.origin.z) / g.h - 0.5f), 0, g.nz - 1);
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                int bi = x + g.nx * z;
                (buckets[bi] ??= new List<int>()).Add(t);
            }
        }

        var hits = new List<float>();
        // Tiny irrational nudge so rays don't run exactly along shared triangle edges.
        float jx = g.h * 0.000731f, jz = g.h * 0.000419f;
        for (int x = 0; x < g.nx; x++)
        for (int z = 0; z < g.nz; z++)
        {
            var bucket = buckets[x + g.nx * z];
            if (bucket == null) continue;
            float px = g.origin.x + (x + 0.5f) * g.h + jx;
            float pz = g.origin.z + (z + 0.5f) * g.h + jz;

            hits.Clear();
            foreach (int t in bucket)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                if (VerticalRayHit(px, pz, a, b, c, out float y)) hits.Add(y);
            }
            if (hits.Count < 2 || hits.Count % 2 != 0) continue;
            hits.Sort();

            for (int k = 0; k + 1 < hits.Count; k += 2)
            {
                int y0 = Mathf.Max(0, Mathf.CeilToInt((hits[k] - g.origin.y) / g.h - 0.5f));
                int y1 = Mathf.Min(g.ny - 1, Mathf.FloorToInt((hits[k + 1] - g.origin.y) / g.h - 0.5f));
                for (int y = y0; y <= y1; y++) g.solid[g.Index(x, y, z)] = true;
            }
        }
    }

    // Where a vertical line through (px, pz) crosses triangle abc, if it does.
    static bool VerticalRayHit(float px, float pz, Vector3 a, Vector3 b, Vector3 c, out float y)
    {
        y = 0f;
        float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
        if (Mathf.Abs(d) < 1e-12f) return false; // triangle is edge-on to the ray
        float w0 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / d;
        float w1 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / d;
        float w2 = 1f - w0 - w1;
        if (w0 < 0f || w1 < 0f || w2 < 0f) return false;
        y = w0 * a.y + w1 * b.y + w2 * c.y;
        return true;
    }

    // Shell = solid voxels with at least one empty (or out-of-grid) face neighbour.
    static void ComputeShell(Grid g)
    {
        g.shell = new bool[g.solid.Length];
        g.SolidCount = 0;
        g.ShellCount = 0;
        for (int z = 0; z < g.nz; z++)
        for (int y = 0; y < g.ny; y++)
        for (int x = 0; x < g.nx; x++)
        {
            int i = g.Index(x, y, z);
            if (!g.solid[i]) continue;
            g.SolidCount++;
            if (!Solid(g, x + 1, y, z) || !Solid(g, x - 1, y, z) ||
                !Solid(g, x, y + 1, z) || !Solid(g, x, y - 1, z) ||
                !Solid(g, x, y, z + 1) || !Solid(g, x, y, z - 1))
            {
                g.shell[i] = true;
                g.ShellCount++;
            }
        }
    }

    static bool Solid(Grid g, int x, int y, int z) => g.InBounds(x, y, z) && g.solid[g.Index(x, y, z)];

    // ------------------------------------------------------------------ result

    static Result BuildResult(Grid g, int cost, float cubeSize, VOBFormation.PivotMode pivot, int resolution)
    {
        var shell = new List<Vector3Int>();
        var interior = new List<Vector3Int>();
        var depth = new int[g.solid.Length];
        var frontier = new Queue<Vector3Int>();

        for (int z = 0; z < g.nz; z++)
        for (int y = 0; y < g.ny; y++)
        for (int x = 0; x < g.nx; x++)
        {
            int i = g.Index(x, y, z);
            depth[i] = -1;
            if (!g.solid[i]) continue;
            if (g.shell[i])
            {
                shell.Add(new Vector3Int(x, y, z));
                depth[i] = 0;
                frontier.Enqueue(new Vector3Int(x, y, z));
            }
        }

        // Depth below the surface for every interior voxel (BFS from the shell), so spare cubes pack shallow-first.
        var dirs = new[]
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };
        while (frontier.Count > 0)
        {
            var v = frontier.Dequeue();
            int dv = depth[g.Index(v.x, v.y, v.z)];
            foreach (var d in dirs)
            {
                var n = v + d;
                if (!g.InBounds(n.x, n.y, n.z)) continue;
                int ni = g.Index(n.x, n.y, n.z);
                if (!g.solid[ni] || depth[ni] >= 0) continue;
                depth[ni] = dv + 1;
                interior.Add(n);
                frontier.Enqueue(n);
            }
        }
        // BFS order is already shallowest-first.

        // Shape = shell, then shallow interior, until the cost is met. Whatever interior is left is reserve space.
        var shapeVoxels = new List<Vector3Int>(cost);
        int shellTaken = Mathf.Min(shell.Count, cost);
        for (int i = 0; i < shellTaken; i++) shapeVoxels.Add(shell[i]);
        int interiorTaken = Mathf.Min(interior.Count, cost - shapeVoxels.Count);
        for (int i = 0; i < interiorTaken; i++) shapeVoxels.Add(interior[i]);

        // Thin shapes can run out of voxels before the cost is met: stack the extra cubes inside existing
        // ones (deepest first) so they're hidden but still part of the shape's mass.
        int duplicates = 0;
        var pool = interiorTaken > 0 ? interior : shell;
        int poolCount = interiorTaken > 0 ? interiorTaken : shellTaken;
        while (shapeVoxels.Count < cost && poolCount > 0)
        {
            shapeVoxels.Add(pool[poolCount - 1 - (duplicates % poolCount)]);
            duplicates++;
        }

        var reserveVoxels = interior.GetRange(interiorTaken, interior.Count - interiorTaken);

        // Pivot, then scale so one voxel = one cube.
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var v in shapeVoxels)
        {
            var c = g.Center(v.x, v.y, v.z);
            min = Vector3.Min(min, c);
            max = Vector3.Max(max, c);
        }
        Vector3 pivotPoint = (min + max) * 0.5f;
        if (pivot == VOBFormation.PivotMode.Bottom) pivotPoint.y = min.y - g.h * 0.5f;

        float scale = cubeSize / g.h;
        Vector3 ToSlot(Vector3Int v) => (g.Center(v.x, v.y, v.z) - pivotPoint) * scale;

        var result = new Result
        {
            slots = new Vector3[shapeVoxels.Count],
            reserve = new Vector3[reserveVoxels.Count],
            resolution = resolution,
            surfaceCount = shellTaken,
            interiorCount = interiorTaken,
            duplicateCount = duplicates
        };
        for (int i = 0; i < shapeVoxels.Count; i++) result.slots[i] = ToSlot(shapeVoxels[i]);
        for (int i = 0; i < reserveVoxels.Count; i++) result.reserve[i] = ToSlot(reserveVoxels[i]);

        if (shell.Count > cost)
            result.error = $"Even the coarsest grid needs {shell.Count} cubes for the outside; the shape has holes. Raise the cube cost.";
        return result;
    }
}
