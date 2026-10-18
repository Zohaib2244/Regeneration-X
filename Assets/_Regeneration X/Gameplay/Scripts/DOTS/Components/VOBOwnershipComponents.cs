using Unity.Entities;
using Unity.Mathematics;

// Ownership model
// ---------------
// A VOB belongs to whichever VOBY its VOBComponent.VOBYParent points at.
// A VOB with VOBLoose belongs to nobody: it's a physics pickup lying in the world.
// Collecting = a VOBCollector takes ownership of a loose VOB. Losing = DetachVOBs makes owned VOBs loose.

/// <summary>On a VOB that nobody owns. It can be collected once the elapsed time passes PickupAfter.</summary>
public struct VOBLoose : IComponentData
{
    public double PickupAfter;
}

/// <summary>Marks the VOBY the player controls.</summary>
public struct PlayerVOBYTag : IComponentData { }

/// <summary>
/// Every spot a VOBY can put its cubes, in priority order and in the VOBY's local space:
/// shape slots first, then hidden reserve slots inside the shape, then the spare-cube halo.
/// </summary>
[InternalBufferCapacity(0)]
public struct VOBYSlot : IBufferElementData
{
    public float3 Position;
    public quaternion Rotation;
}

/// <summary>Bookkeeping for the VOBY's current formation.</summary>
public struct VOBYFormationInfo : IComponentData
{
    /// <summary>Cube cost of the current shape (how many leading VOBYSlots are the visible shape).</summary>
    public int ShapeSlots;
    /// <summary>Index of the next free VOBYSlot. A newly collected cube goes here.</summary>
    public int NextFree;
    /// <summary>Radius around the VOBY origin that holds the shape.</summary>
    public float ShapeRadius;
    /// <summary>Radius around the VOBY origin that holds every occupied slot, halo included.</summary>
    public float OuterRadius;
    public float CubeSize;
}

/// <summary>How many non-loose VOBs this VOBY owns right now. Kept up to date by VOBYCubeCountSystem.</summary>
public struct VOBYCubeCount : IComponentData
{
    public int Value;
}

/// <summary>Lets a VOBY pull in and absorb loose VOBs.</summary>
public struct VOBCollector : IComponentData
{
    public bool Enabled;
    /// <summary>Loose VOBs closer than this start getting pulled in.</summary>
    public float Radius;
    /// <summary>Speed a pulled cube flies at.</summary>
    public float PullSpeed;
    /// <summary>Absorbed once within OuterRadius + this.</summary>
    public float AbsorbMargin;
    /// <summary>Seconds an absorbed cube takes to fly into its slot.</summary>
    public float AbsorbAnimationDuration;
}
