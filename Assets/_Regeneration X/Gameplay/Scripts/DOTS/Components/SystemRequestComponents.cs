using Unity.Entities;
using Unity.Mathematics;


// ExplosionRequest component
public struct ExplosionRequest : IComponentData
{
    public float Radius;
    public float Force;
    public float RotationAmount;
}

public struct VOBYReconstructionRequest : IComponentData
{
}
public struct VOBReconstructionProcess : IComponentData
{
    public float Timer;
    public int NextAnimationIndex;
    public int BatchSize;
    public float BatchDelay;
}