using UnityEngine;

/// <summary>
/// A shape the VOBs can turn into.
/// <see cref="slots"/> is the shape itself: its length is the shape's cube cost.
/// <see cref="reserveSlots"/> are hidden spots inside the shape (shallowest first) where spare cubes
/// are packed when the owner has more cubes than the cost. Anything beyond that floats in a halo.
/// Bake one from any mesh with NuttyTools > VOB Formation Baker, or save a hand-made VOBY layout
/// with "Save Layout As Formation" on VOBYAuthoring.
/// </summary>
[CreateAssetMenu(menuName = "Regeneration X/VOB Formation", fileName = "NewFormation")]
public class VOBFormation : ScriptableObject
{
    public enum FillMode
    {
        [Tooltip("Highest detail for the cost: cubes cover the outside first, the rest fill in just under the surface.")]
        Hollow,
        [Tooltip("Lower detail, but the whole volume is cubes - chunkier and heavier when it explodes.")]
        Solid
    }

    public enum PivotMode
    {
        Center,
        [Tooltip("Shape stands on the VOBY's origin. Good for characters and props.")]
        Bottom
    }

    [Tooltip("Local position of each cube of the shape, relative to the VOBY root. Length = cube cost.")]
    public Vector3[] slots = new Vector3[0];

    [Tooltip("Optional local rotation per slot. Empty means every cube is axis-aligned.")]
    public Quaternion[] rotations = new Quaternion[0];

    [Tooltip("Hidden interior spots for spare cubes, shallowest first.")]
    public Vector3[] reserveSlots = new Vector3[0];

    [Header("Bake info (read only)")]
    public float cubeSize = 0.25f;
    public int surfaceSlots;
    public int interiorSlots;
    public int duplicateSlots;
    public int resolution;

    [Header("Bake settings (used by Re-bake)")]
    [Tooltip("Asset GUID of the mesh/prefab this was baked from. Kept as a GUID so the source model is not pulled into builds.")]
    public string sourceGuid;
    public FillMode fillMode = FillMode.Hollow;
    public PivotMode pivotMode = PivotMode.Center;

    /// <summary>How many cubes the owner needs to take this shape.</summary>
    public int Cost => slots != null ? slots.Length : 0;

    public Quaternion GetRotation(int i)
    {
        if (rotations == null || i >= rotations.Length) return Quaternion.identity;
        return rotations[i];
    }

    /// <summary>Radius of a sphere around the VOBY origin that contains every slot.</summary>
    public float BoundsRadius
    {
        get
        {
            float r = 0f;
            if (slots != null)
                foreach (var s in slots) r = Mathf.Max(r, s.magnitude);
            return r + cubeSize * 0.5f;
        }
    }
}
