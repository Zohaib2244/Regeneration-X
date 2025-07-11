using Unity.Entities;
using Unity.Mathematics;


public struct VOBComponent : IComponentData
{
    public Entity VOBYParent;
    public float3 Position;
    public quaternion Rotation;
    public int VOBIndex;
}