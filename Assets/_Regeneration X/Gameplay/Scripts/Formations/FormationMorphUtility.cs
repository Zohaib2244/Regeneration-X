using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Decides which cube goes to which slot of a formation.
/// Cubes are matched region-to-region (top of the old shape -> top of the new one), then nearby
/// pairs are swapped when that shortens the trip, so cubes fly in roughly parallel paths instead
/// of criss-crossing through each other.
/// </summary>
public static class FormationAssigner
{
    /// <returns>For each index in <paramref name="from"/>, the index in <paramref name="to"/> it should travel to.
    /// When there are more cubes than slots, some slots receive more than one cube.</returns>
    public static int[] Assign(float3[] from, float3[] to, int refinePassesPerCube = 8, int window = 32, uint seed = 1234)
    {
        int n = from.Length, m = to.Length;
        var result = new int[n];
        if (n == 0 || m == 0) return result;

        int[] fromOrder = SortByMorton(from);
        int[] toOrder = SortByMorton(to);

        // Rank-to-rank pairing (stretched if the counts differ).
        for (int r = 0; r < n; r++)
            result[fromOrder[r]] = toOrder[(int)((long)r * m / n)];

        // Local refinement: swap targets between cubes that are close in Morton order when it helps.
        var rng = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
        int iterations = n * refinePassesPerCube;
        for (int k = 0; k < iterations; k++)
        {
            int ra = rng.NextInt(0, n);
            int rb = math.clamp(ra + rng.NextInt(-window, window + 1), 0, n - 1);
            if (ra == rb) continue;

            int a = fromOrder[ra], b = fromOrder[rb];
            int ta = result[a], tb = result[b];
            float before = math.distancesq(from[a], to[ta]) + math.distancesq(from[b], to[tb]);
            float after = math.distancesq(from[a], to[tb]) + math.distancesq(from[b], to[ta]);
            if (after < before)
            {
                result[a] = tb;
                result[b] = ta;
            }
        }
        return result;
    }

    // Indices of points sorted along a Z-order curve, with each set normalised to its own bounds
    // so that "upper-left-front" of one shape lines up with "upper-left-front" of the other.
    static int[] SortByMorton(float3[] points)
    {
        int n = points.Length;
        float3 min = points[0], max = points[0];
        for (int i = 1; i < n; i++)
        {
            min = math.min(min, points[i]);
            max = math.max(max, points[i]);
        }
        float3 extent = math.max(max - min, new float3(1e-5f));

        var keys = new uint[n];
        var order = new int[n];
        for (int i = 0; i < n; i++)
        {
            uint3 q = (uint3)math.clamp((points[i] - min) / extent * 1023f, 0f, 1023f);
            keys[i] = Spread(q.y) << 2 | Spread(q.x) << 1 | Spread(q.z);
            order[i] = i;
        }
        Array.Sort(keys, order);
        return order;
    }

    // Spreads the low 10 bits of x so there are two zero bits between each.
    static uint Spread(uint x)
    {
        x &= 0x3ff;
        x = (x | (x << 16)) & 0x030000FF;
        x = (x | (x << 8)) & 0x0300F00F;
        x = (x | (x << 4)) & 0x030C30C3;
        x = (x | (x << 2)) & 0x09249249;
        return x;
    }
}

public enum MorphResult
{
    Started,
    NotEnoughCubes,
    Busy,
    Invalid
}

/// <summary>
/// Main-thread helpers for morphing, counting, and detaching a VOBY's cubes.
/// Called from MonoBehaviours (ECSCommandBridge); everything per-frame lives in the DOTS systems.
/// </summary>
public static class FormationMorphUtility
{
    /// <summary>Extra slots reserved past the owner's current count, so collected cubes have somewhere to go.</summary>
    public const int DefaultSpareCapacity = 2048;

    /// <summary>The player VOBY if one is tagged, otherwise the first VOBY found. Entity.Null if none.</summary>
    public static Entity FindControlledVOBY(EntityManager em)
    {
        var player = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerVOBYTag>());
        Entity result = Entity.Null;
        if (!player.IsEmpty)
        {
            var arr = player.ToEntityArray(Allocator.Temp);
            result = arr[0];
            arr.Dispose();
        }
        player.Dispose();
        if (result != Entity.Null) return result;

        var any = em.CreateEntityQuery(ComponentType.ReadOnly<VOBYComponent>());
        if (!any.IsEmpty)
        {
            var arr = any.ToEntityArray(Allocator.Temp);
            result = arr[0];
            arr.Dispose();
        }
        any.Dispose();
        return result;
    }

    /// <summary>How many cubes the VOBY owns (loose pickups excluded).</summary>
    public static int GetCubeCount(EntityManager em, Entity owner)
    {
        if (owner == Entity.Null || !em.Exists(owner)) return 0;
        if (em.HasComponent<VOBYCubeCount>(owner))
            return em.GetComponentData<VOBYCubeCount>(owner).Value;

        var owned = GetOwnedVOBs(em, owner);
        int n = owned.Count;
        return n;
    }

    /// <summary>True while a reconstruction/morph is still being handed out in batches.</summary>
    public static bool IsBusy(EntityManager em)
    {
        var process = em.CreateEntityQuery(ComponentType.ReadOnly<VOBReconstructionProcess>());
        var request = em.CreateEntityQuery(ComponentType.ReadOnly<VOBYReconstructionRequest>());
        bool busy = !process.IsEmpty || !request.IsEmpty;
        process.Dispose();
        request.Dispose();
        return busy;
    }

    /// <summary>
    /// Points every cube <paramref name="owner"/> owns at <paramref name="formation"/>: the shape first,
    /// spare cubes packed inside it, anything past that in a halo. The VOBs are tagged for the
    /// reconstruction pass; the caller then sends a VOBYReconstructionRequest with useAssignedTargets.
    /// </summary>
    /// <param name="missing">How many more cubes are needed when the result is NotEnoughCubes.</param>
    public static MorphResult PrepareMorph(EntityManager em, Entity owner, VOBFormation formation, out int missing,
        int spareCapacity = DefaultSpareCapacity)
    {
        missing = 0;
        if (formation == null || formation.Cost == 0 || owner == Entity.Null || !em.Exists(owner))
            return MorphResult.Invalid;
        if (IsBusy(em))
            return MorphResult.Busy;

        List<(Entity entity, VOBComponent vob, float3 worldPos)> owned = GetOwnedVOBs(em, owner);
        int k = owned.Count;
        if (k < formation.Cost)
        {
            missing = formation.Cost - k;
            return MorphResult.NotEnoughCubes;
        }

        // Every spot this VOBY can use, in priority order.
        List<VOBYSlot> layout = FormationSlotLayout.Build(formation.slots, formation.rotations, formation.reserveSlots,
            formation.cubeSize, k + math.max(0, spareCapacity), out float shapeRadius);

        DynamicBuffer<VOBYSlot> buffer = em.HasBuffer<VOBYSlot>(owner) ? em.GetBuffer<VOBYSlot>(owner) : em.AddBuffer<VOBYSlot>(owner);
        buffer.Clear();
        foreach (var slot in layout) buffer.Add(slot);

        float4x4 ownerMatrix = float4x4.identity;
        quaternion ownerRotation = quaternion.identity;
        if (em.HasComponent<LocalToWorld>(owner))
        {
            var ltw = em.GetComponentData<LocalToWorld>(owner);
            ownerMatrix = ltw.Value;
            ownerRotation = ltw.Rotation;
        }

        // The first k spots get the k cubes.
        var from = new float3[k];
        var to = new float3[k];
        float outerRadius = shapeRadius;
        for (int i = 0; i < k; i++)
        {
            from[i] = owned[i].worldPos;
            to[i] = math.transform(ownerMatrix, layout[i].Position);
            outerRadius = math.max(outerRadius, math.length(layout[i].Position) + formation.cubeSize * 0.5f);
        }
        int[] pick = FormationAssigner.Assign(from, to);

        var needTag = new NativeList<Entity>(k, Allocator.Temp);
        for (int i = 0; i < k; i++)
        {
            var vob = owned[i].vob;
            vob.Position = to[pick[i]];
            vob.Rotation = math.mul(ownerRotation, layout[pick[i]].Rotation);
            em.SetComponentData(owned[i].entity, vob);
            if (!em.HasComponent<VOBExplodedTag>(owned[i].entity))
                needTag.Add(owned[i].entity);
        }
        // Intact cubes become eligible for the reconstruction pass, so a shape can morph straight into another.
        if (needTag.Length > 0)
            em.AddComponent<VOBExplodedTag>(needTag.AsArray());
        needTag.Dispose();

        var info = new VOBYFormationInfo
        {
            ShapeSlots = formation.Cost,
            NextFree = k,
            ShapeRadius = shapeRadius,
            OuterRadius = outerRadius,
            CubeSize = formation.cubeSize
        };
        if (em.HasComponent<VOBYFormationInfo>(owner)) em.SetComponentData(owner, info);
        else em.AddComponentData(owner, info);

        return MorphResult.Started;
    }

    /// <summary>
    /// Knocks <paramref name="count"/> cubes off <paramref name="owner"/> - the ones closest to
    /// <paramref name="hitPoint"/> - and turns them into loose physics pickups flying away from the hit.
    /// This is the hook for damage: call it with the hit position and however many cubes the hit is worth.
    /// </summary>
    /// <param name="pickupDelay">Seconds before the knocked-off cubes can be collected again.</param>
    /// <returns>How many cubes were actually detached.</returns>
    public static int DetachVOBs(EntityManager em, Entity owner, int count, float3 hitPoint, float force,
        float pickupDelay = 1.5f, uint seed = 0)
    {
        if (count <= 0 || owner == Entity.Null) return 0;

        var owned = GetOwnedVOBs(em, owner);
        if (owned.Count == 0) return 0;

        // Closest to the hit first.
        var keys = new float[owned.Count];
        var order = new int[owned.Count];
        for (int i = 0; i < owned.Count; i++)
        {
            keys[i] = math.distancesq(owned[i].worldPos, hitPoint);
            order[i] = i;
        }
        Array.Sort(keys, order);

        int n = math.min(count, owned.Count);
        double pickupAfter = em.World.Time.ElapsedTime + pickupDelay;
        var rng = new Unity.Mathematics.Random(seed != 0 ? seed : (uint)(UnityEngine.Random.Range(1, int.MaxValue)));
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int j = 0; j < n; j++)
        {
            var entry = owned[order[j]];
            Entity e = entry.entity;
            var ltw = em.GetComponentData<LocalToWorld>(e);
            float scale = em.HasComponent<LocalTransform>(e) ? em.GetComponentData<LocalTransform>(e).Scale : 1f;

            // Unparent at the current world pose.
            ecb.SetComponent(e, new Parent { Value = Entity.Null });
            ecb.SetComponent(e, new LocalTransform { Position = ltw.Position, Rotation = ltw.Rotation, Scale = scale });

            var vob = entry.vob;
            vob.VOBYParent = Entity.Null;
            ecb.SetComponent(e, vob);

            ecb.RemoveComponent<VOBExplodedTag>(e);
            ecb.RemoveComponent<VOBDefaultReconstructionAnimation>(e);
            ecb.RemoveComponent<VOBSpiralReconstructionAnimation>(e);
            ecb.AddComponent(e, new VOBLoose { PickupAfter = pickupAfter });

            // Same body setup the explosion uses.
            float3 away = math.normalizesafe(ltw.Position - hitPoint, new float3(0, 1, 0));
            float3 velocity = (away + rng.NextFloat3Direction() * 0.35f + new float3(0, 0.4f, 0)) * force;
            ecb.AddComponent(e, new PhysicsMass
            {
                Transform = RigidTransform.identity,
                InverseMass = 1f,
                InverseInertia = new float3(1f),
                AngularExpansionFactor = 0f
            });
            ecb.AddComponent(e, new PhysicsGravityFactor { Value = 1f });
            ecb.AddComponent(e, new PhysicsVelocity { Linear = velocity, Angular = rng.NextFloat3Direction() * 6f });
        }

        ecb.Playback(em);
        ecb.Dispose();
        return n;
    }

    // Owned, non-loose VOBs of one VOBY with their current world position.
    static List<(Entity entity, VOBComponent vob, float3 worldPos)> GetOwnedVOBs(EntityManager em, Entity owner)
    {
        var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<VOBComponent>(), ComponentType.ReadOnly<LocalToWorld>() },
            None = new[] { ComponentType.ReadOnly<VOBLoose>() }
        });
        var entities = query.ToEntityArray(Allocator.Temp);
        var vobs = query.ToComponentDataArray<VOBComponent>(Allocator.Temp);
        var ltws = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

        var result = new List<(Entity, VOBComponent, float3)>(entities.Length);
        for (int i = 0; i < entities.Length; i++)
            if (vobs[i].VOBYParent == owner)
                result.Add((entities[i], vobs[i], ltws[i].Position));

        entities.Dispose();
        vobs.Dispose();
        ltws.Dispose();
        query.Dispose();
        return result;
    }
}
