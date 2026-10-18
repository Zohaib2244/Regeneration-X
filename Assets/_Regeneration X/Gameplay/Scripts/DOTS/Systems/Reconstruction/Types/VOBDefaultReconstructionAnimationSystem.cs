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

        foreach (var (anim, entity) in SystemAPI.Query<RefRW<VOBDefaultReconstructionAnimation>>().WithEntityAccess())
        {
            // Wait for delay before starting animation
            if (anim.ValueRW.AnimationTime < anim.ValueRO.DelayTime)
            {
                anim.ValueRW.AnimationTime += deltaTime;
                continue;
            }

            float t = math.saturate((anim.ValueRW.AnimationTime - anim.ValueRO.DelayTime) / anim.ValueRO.AnimationDuration);

            // Resolve the target in world space (it may be stored relative to the parent VOBY)
            float3 targetPos = anim.ValueRO.TargetPosition;
            quaternion targetRot = anim.ValueRO.TargetRotation;
            if (anim.ValueRO.TargetIsLocal && SystemAPI.HasComponent<Parent>(entity))
            {
                Entity owner = SystemAPI.GetComponentRO<Parent>(entity).ValueRO.Value;
                if (owner != Entity.Null && SystemAPI.HasComponent<LocalToWorld>(owner))
                {
                    var ownerLTW = SystemAPI.GetComponentRO<LocalToWorld>(owner).ValueRO;
                    targetPos = math.transform(ownerLTW.Value, targetPos);
                    targetRot = math.mul(ownerLTW.Rotation, targetRot);
                }
            }

            // Interpolate in world space
            float3 worldPos = math.lerp(anim.ValueRO.StartPosition, targetPos, t);
            quaternion worldRot = math.slerp(anim.ValueRO.StartRotation, targetRot, t);

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
                ecb.RemoveComponent<VOBDefaultReconstructionAnimation>(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}