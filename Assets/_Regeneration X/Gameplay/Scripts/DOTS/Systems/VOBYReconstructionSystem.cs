using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using Unity.Mathematics;

public partial struct VOBYReconstructionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<VOBReconstructionProcess>();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        // Get the singleton and update its timer
        var processEntity = SystemAPI.GetSingletonEntity<VOBReconstructionProcess>();
        var process = SystemAPI.GetComponentRW<VOBReconstructionProcess>(processEntity);
        process.ValueRW.Timer += SystemAPI.Time.DeltaTime;

        // If the timer hasn't reached the next batch delay, do nothing
        if (process.ValueRO.Timer < process.ValueRO.BatchDelay)
        {
            return;
        }

        // Timer has been reached, reset it for the next batch
        process.ValueRW.Timer = 0f;

        int animatedThisFrame = 0;
        int startIndex = process.ValueRO.NextAnimationIndex;

        // Query for all VOBs that still have physics components
        foreach (var (vob, transform, entity) in SystemAPI
            .Query<RefRO<VOBComponent>, RefRO<LocalTransform>>()
            .WithAll<PhysicsVelocity, PhysicsMass, PhysicsGravityFactor>()
            .WithEntityAccess())
        {
            // This logic assumes VOBs are processed in a deterministic order.
            // A more robust solution might involve tagging VOBs with an index on creation.
            if (animatedThisFrame < process.ValueRO.BatchSize)
            {
                // Remove physics components
                ecb.RemoveComponent<PhysicsVelocity>(entity);
                ecb.RemoveComponent<PhysicsMass>(entity);
                ecb.RemoveComponent<PhysicsGravityFactor>(entity);
                
                // Reparent to original VOBY
                ecb.AddComponent(entity, new Parent { Value = vob.ValueRO.VOBYParent });

                // Add animation component to start animating now
                ecb.AddComponent(entity, new VOBReconstructionAnimation
                {
                    StartPosition = transform.ValueRO.Position,
                    StartRotation = transform.ValueRO.Rotation,
                    TargetPosition = vob.ValueRO.Position,
                    TargetRotation = vob.ValueRO.Rotation,
                    AnimationTime = 0f,
                    DelayTime = 0f, // No delay, starts immediately
                    AnimationDuration = 0.25f, // 1 second animation
                    AnimationIndex = startIndex + animatedThisFrame
                });

                animatedThisFrame++;
            }
        }

        if (animatedThisFrame > 0)
        {
            // Update the index for the next batch
            process.ValueRW.NextAnimationIndex += animatedThisFrame;
        }
        else
        {
            // No more VOBs to animate, end the process
            ecb.DestroyEntity(processEntity);
        }
    }
}

// This new system will handle the initial request and set up the process
public partial struct VOBYReconstructionRequestHandlerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<VOBYReconstructionRequest>()));
    }

    public void OnUpdate(ref SystemState state)
    {
        // Check if a process is already running
        if (SystemAPI.HasSingleton<VOBReconstructionProcess>())
        {
            // Destroy any new requests if a process is active
            foreach (var (_, requestEntity) in SystemAPI.Query<VOBYReconstructionRequest>().WithEntityAccess())
            {
                state.EntityManager.DestroyEntity(requestEntity);
            }
            return;
        }

        var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        // Only run if a request exists
        foreach (var (_, requestEntity) in SystemAPI.Query<VOBYReconstructionRequest>().WithEntityAccess())
        {
            // Create the singleton to manage the process
            var processEntity = ecb.CreateEntity();
            ecb.AddComponent(processEntity, new VOBReconstructionProcess
            {
                Timer = 0f,
                NextAnimationIndex = 0,
                BatchSize = 50,
                BatchDelay = 0.05f // 50ms delay between batches
            });

            // Destroy the request entity
            ecb.DestroyEntity(requestEntity);
        }
    }
}