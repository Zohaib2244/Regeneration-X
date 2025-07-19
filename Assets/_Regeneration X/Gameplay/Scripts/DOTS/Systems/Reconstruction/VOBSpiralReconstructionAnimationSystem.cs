using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct VOBSpiralReconstructionAnimationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (anim, entity) in SystemAPI.Query<RefRW<VOBSpiralReconstructionAnimation>>().WithEntityAccess())
        {
            // Wait for delay before starting animation
            if (anim.ValueRW.AnimationTime < anim.ValueRO.DelayTime)
            {
                anim.ValueRW.AnimationTime += deltaTime;
                continue;
            }

            float elapsed = anim.ValueRW.AnimationTime - anim.ValueRO.DelayTime;
            float totalDuration = anim.ValueRO.SpiralDuration + anim.ValueRO.MoveDuration;

            float3 worldPos;
            quaternion worldRot;

            if (elapsed < anim.ValueRO.SpiralDuration)
            {
                // Spiral phase - move from current position to spiral, then spiral upward
                float spiralT = elapsed / anim.ValueRO.SpiralDuration;
                
                // Calculate target position in spiral
                float angle = spiralT * anim.ValueRO.SpiralRotations * 2 * math.PI;
                float3 spiralOffset = new float3(
                    math.cos(angle) * anim.ValueRO.SpiralRadius,
                    spiralT * anim.ValueRO.SpiralHeight, // Gradually spiral upward
                    math.sin(angle) * anim.ValueRO.SpiralRadius
                );
                
                float3 spiralWorldPos = anim.ValueRO.SpiralCenter + spiralOffset;
                
                // Interpolate from start position to spiral position
                worldPos = math.lerp(anim.ValueRO.StartPosition, spiralWorldPos, spiralT);
                
                // Rotate the VOB as it spirals
                float rotationAngle = angle * 0.5f;
                worldRot = math.slerp(anim.ValueRO.StartRotation, 
                    math.mul(anim.ValueRO.StartRotation, quaternion.RotateY(rotationAngle)), spiralT);
            }
            else
            {
                // Move to target phase - from final spiral position to target
                float moveElapsed = elapsed - anim.ValueRO.SpiralDuration;
                float moveT = math.saturate(moveElapsed / anim.ValueRO.MoveDuration);
                
                // Get final spiral position (at the top of the spiral)
                float finalAngle = anim.ValueRO.SpiralRotations * 2 * math.PI;
                float3 finalSpiralOffset = new float3(
                    math.cos(finalAngle) * anim.ValueRO.SpiralRadius,
                    anim.ValueRO.SpiralHeight, // At full height
                    math.sin(finalAngle) * anim.ValueRO.SpiralRadius
                );
                float3 finalSpiralPos = anim.ValueRO.SpiralCenter + finalSpiralOffset;
                
                // Move from final spiral position to target
                worldPos = math.lerp(finalSpiralPos, anim.ValueRO.TargetPosition, moveT);
                worldRot = math.slerp(anim.ValueRO.StartRotation, anim.ValueRO.TargetRotation, moveT);
            }

            // Apply to transform - ALWAYS work in world space first, then convert to local
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
                    
                    // Ensure parent matrix is valid
                    if (math.determinant(parentLTW.Value) != 0)
                    {
                        float4x4 parentInverse = math.inverse(parentLTW.Value);
                        
                        // Transform world position to local space
                        float3 localPos = math.transform(parentInverse, worldPos);
                        quaternion localRot = math.mul(math.inverse(parentLTW.Rotation), worldRot);

                        localTransform.ValueRW.Position = localPos;
                        localTransform.ValueRW.Rotation = localRot;
                    }
                    else
                    {
                        // Fallback: use world values directly if parent matrix is invalid
                        localTransform.ValueRW.Position = worldPos;
                        localTransform.ValueRW.Rotation = worldRot;
                    }
                }
                else
                {
                    // No parent or parent doesn't have LocalToWorld, use world values directly
                    localTransform.ValueRW.Position = worldPos;
                    localTransform.ValueRW.Rotation = worldRot;
                }
            }

            anim.ValueRW.AnimationTime += deltaTime;

            // Check if animation is complete
            if (elapsed >= totalDuration)
            {
                ecb.RemoveComponent<VOBSpiralReconstructionAnimation>(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}