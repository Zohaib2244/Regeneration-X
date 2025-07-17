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

            // Interpolate in world space
            float3 worldPos = math.lerp(anim.ValueRO.StartPosition, anim.ValueRO.TargetPosition, t);
            quaternion worldRot = math.slerp(anim.ValueRO.StartRotation, anim.ValueRO.TargetRotation, t);

            // Apply to transform
            if (SystemAPI.HasComponent<LocalTransform>(entity))
            {
                var localTransform = SystemAPI.GetComponentRW<LocalTransform>(entity);

                // Check if entity has a parent
                Entity parent = Entity.Null;
                if (SystemAPI.HasComponent<Parent>(entity))
                    parent = SystemAPI.GetComponentRO<Parent>(entity).ValueRO.Value;

                if (parent != Entity.Null && SystemAPI.HasComponent<LocalToWorld>(parent))
                {
                    // Convert world space to local space relative to parent
                    var parentLTW = SystemAPI.GetComponentRO<LocalToWorld>(parent).ValueRO;
                    float4x4 parentInverse = math.inverse(parentLTW.Value);

                    // Transform world position to local space
                    float3 localPos = math.transform(parentInverse, worldPos);
                    quaternion localRot = math.mul(math.inverse(parentLTW.Rotation), worldRot);

                    localTransform.ValueRW.Position = localPos;
                    localTransform.ValueRW.Rotation = localRot;
                }
                else
                {
                    // No parent or parent doesn't have LocalToWorld, use world values directly
                    localTransform.ValueRW.Position = worldPos;
                    localTransform.ValueRW.Rotation = worldRot;
                }
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