using Unity.Entities;
using Unity.Mathematics;


#region ExplosionRequest

//* ExplosionRequest component
public struct ExplosionRequest : IComponentData
{
    public float3 Epicenter;
    public float Radius;
    public float Force;
    public float RotationAmount;
}
#endregion

#region VOBYReconstruction
//* VOBYReconstructionRequest component
public struct VOBYReconstructionRequest : IComponentData
{
    public bool randomizeVOBs; // New field to control randomization of VOBs
    public bool freezeUnbatchedVOBs; // New field to control freezing of unbatched VOBs
    public int batchSize; // Size of each batch for reconstruction
    public float batchDelay; // Delay between batches in seconds
    public float animationDuration; // Duration of the animation for each VOB
}
//* VOBReconstructionProcess component
public struct VOBReconstructionProcess : IComponentData
{
    public float Timer;
    public int NextAnimationIndex;
    public int BatchSize;
    public float BatchDelay;
    public bool FreezeUnbatchedVOBs; // New field to control freezing of unbatched VOBs
    public float AnimationDuration; // Duration of the animation for each VOB
    public bool RandomizeVOBs; // New field to control randomization of VOBs
}
#endregion
#region Magnetism Components

// New magnetic components
public struct MagneticFieldComponent : IComponentData
{
    public float3 Position;
    public float3 Direction; // Direction of the cone
    public float Radius;
    public float Force;
    public float ConeAngle; // Angle of the cone in radians
    public bool IsActive;
}

public struct VOBMagneticForce : IComponentData
{
    public float3 Force;
    public float3 Velocity;
}

public struct MagneticRequest : IComponentData
{
    public float3 MagnetPosition;
    public float3 MagnetDirection;
    public float Radius;
    public float Force;
    public float ConeAngle;
}
#endregion
