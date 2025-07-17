using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct VOBReconstructionAnimationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (anim, entity) in SystemAPI.Query<RefRW<VOBReconstructionAnimation>>().WithEntityAccess())
        {
            // Wait for delay before starting animation
            if (anim.ValueRW.AnimationTime < anim.ValueRO.DelayTime)
            {
                anim.ValueRW.AnimationTime += deltaTime;
                continue;
            }

            float t = math.saturate((anim.ValueRW.AnimationTime - anim.ValueRO.DelayTime) / anim.ValueRO.AnimationDuration);

            // Interpolate position and rotation
            float3 newPos = math.lerp(anim.ValueRO.StartPosition, anim.ValueRO.TargetPosition, t);
            quaternion newRot = math.slerp(anim.ValueRO.StartRotation, anim.ValueRO.TargetRotation, t);

            // Apply to transform
            if (SystemAPI.HasComponent<LocalTransform>(entity))
            {
                var localTransform = SystemAPI.GetComponentRW<LocalTransform>(entity);
                localTransform.ValueRW.Position = newPos;
                localTransform.ValueRW.Rotation = newRot;
            }

            anim.ValueRW.AnimationTime += deltaTime;

            if (t >= 1f)
            {
                // Remove animation component when done
                ecb.RemoveComponent<VOBReconstructionAnimation>(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}