using Unity.Entities;
using Unity.Mathematics;


public struct VOBComponent : IComponentData
{
    public Entity VOBYParent;
    public float3 Position;
    public quaternion Rotation;
    public int VOBIndex;
}
public struct VOBReconstructionAnimation : IComponentData
{
    public float3 StartPosition;
    public quaternion StartRotation;
    public float3 TargetPosition;
    public quaternion TargetRotation;
    public float AnimationTime;
    public float DelayTime;
    public float AnimationDuration;
    public int AnimationIndex;
}