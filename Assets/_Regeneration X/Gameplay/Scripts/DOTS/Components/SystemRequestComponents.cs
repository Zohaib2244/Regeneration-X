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