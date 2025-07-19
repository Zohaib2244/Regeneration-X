using Unity.Entities;
using Unity.Mathematics;

public struct VOBExplodedTag : IComponentData{}
public struct VOBComponent : IComponentData
{
    public Entity VOBYParent;
    public float3 Position;
    public quaternion Rotation;
    public int VOBIndex;
}
public struct VOBDefaultReconstructionAnimation : IComponentData
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
[System.Serializable]
public struct VOBSpiralReconstructionAnimation : IComponentData
{
    public float3 StartPosition;
    public quaternion StartRotation;
    public float3 TargetPosition;
    public quaternion TargetRotation;
    public float3 SpiralCenter;      // Center point of the spiral
    public float SpiralRadius;       // Radius of the spiral
    public float SpiralHeight;       // Height of the spiral
    public float SpiralRotations;    // Number of rotations in the spiral
    public float AnimationTime;
    public float DelayTime;
    public float SpiralDuration;     // Time spent in spiral phase
    public float MoveDuration;       // Time spent moving to target
    public int AnimationIndex;
}