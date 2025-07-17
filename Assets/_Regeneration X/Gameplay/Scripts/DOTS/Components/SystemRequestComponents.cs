using Unity.Entities;
using Unity.Mathematics;


// ExplosionRequest component
public struct ExplosionRequest : IComponentData
{
    public float3 Epicenter;
    public float Radius;
    public float Force;
    public float RotationAmount;
}

public struct VOBYReconstructionRequest : IComponentData
{
    public bool freezeUnbatchedVOBs; // New field to control freezing of unbatched VOBs
    public int batchSize; // Size of each batch for reconstruction
    public float batchDelay; // Delay between batches in seconds
    public float animationDuration; // Duration of the animation for each VOB
}
public struct VOBReconstructionProcess : IComponentData
{
    public float Timer;
    public int NextAnimationIndex;
    public int BatchSize;
    public float BatchDelay;
    public bool FreezeUnbatchedVOBs; // New field to control freezing of unbatched VOBs
    public float AnimationDuration; // Duration of the animation for each VOB
}