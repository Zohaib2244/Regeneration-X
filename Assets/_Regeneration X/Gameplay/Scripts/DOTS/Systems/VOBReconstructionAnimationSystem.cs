using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct VOBReconstructionAnimationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (transform, animation, entity) in SystemAPI
            .Query<RefRW<LocalTransform>, RefRW<VOBReconstructionAnimation>>()
            .WithEntityAccess())
        {
            var animationRef = animation.ValueRW;
            
            // Update animation time
            animationRef.AnimationTime += deltaTime;
            
            // Check if delay has passed
            if (animationRef.AnimationTime < animationRef.DelayTime)
                continue;

            // Calculate progress (0 to 1)
            float progress = (animationRef.AnimationTime - animationRef.DelayTime) / animationRef.AnimationDuration;
            progress = math.clamp(progress, 0f, 1f);

            // Smooth easing (ease-out)
            float easedProgress = 1f - (1f - progress) * (1f - progress);

            // Interpolate position and rotation
            float3 currentPosition = math.lerp(animationRef.StartPosition, animationRef.TargetPosition, easedProgress);
            quaternion currentRotation = math.slerp(animationRef.StartRotation, animationRef.TargetRotation, easedProgress);

            // Update transform
            transform.ValueRW = new LocalTransform
            {
                Position = currentPosition,
                Rotation = currentRotation,
                Scale = transform.ValueRO.Scale
            };

            // Remove animation component when complete
            if (progress >= 1f)
            {
                ecb.RemoveComponent<VOBReconstructionAnimation>(entity);
            }
        }
    }
}