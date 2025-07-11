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