using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using Unity.Mathematics;

public partial struct VOBYReconstructionSystem : ISystem
{
    private EntityQuery _vobyReconstructionQuery;
    public void OnCreate(ref SystemState state)
    {
        _vobyReconstructionQuery = state.GetEntityQuery(ComponentType.ReadOnly<VOBYReconstructionRequest>());
        state.RequireForUpdate(_vobyReconstructionQuery);
    }
    
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        // Only run if a request exists
        foreach (var (_, requestEntity) in SystemAPI.Query<VOBYReconstructionRequest>().WithEntityAccess())
        {
            int animationIndex = 0;
            foreach (var (vob, transform, entity) in SystemAPI
                .Query<RefRO<VOBComponent>, RefRO<LocalTransform>>()
                .WithAll<PhysicsVelocity, PhysicsMass, PhysicsGravityFactor>()
                .WithEntityAccess())
            {
                // Remove physics components
                ecb.RemoveComponent<PhysicsVelocity>(entity);
                ecb.RemoveComponent<PhysicsMass>(entity);
                ecb.RemoveComponent<PhysicsGravityFactor>(entity);
                
                // Reparent to original VOBY
                ecb.AddComponent(entity, new Parent { Value = vob.ValueRO.VOBYParent });

                // Add animation component instead of snapping
                ecb.AddComponent(entity, new VOBReconstructionAnimation
                {
                    StartPosition = transform.ValueRO.Position,
                    StartRotation = transform.ValueRO.Rotation,
                    TargetPosition = vob.ValueRO.Position,
                    TargetRotation = vob.ValueRO.Rotation,
                    AnimationTime = 0f,
                    DelayTime = animationIndex * 0.02f, // 20ms delay per VOB
                    AnimationDuration = 1.0f, // 1 second animation
                    AnimationIndex = animationIndex
                });

                animationIndex++;
            }

            // Destroy the request entity
            ecb.DestroyEntity(requestEntity);
        }
    }
}