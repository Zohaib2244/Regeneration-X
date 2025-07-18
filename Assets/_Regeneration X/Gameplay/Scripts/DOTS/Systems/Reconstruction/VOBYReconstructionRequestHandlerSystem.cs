using Unity.Burst;
using Unity.Entities;
public partial struct VOBYReconstructionRequestHandlerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<VOBYReconstructionRequest>()));
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Check if a process is already running
        if (SystemAPI.HasSingleton<VOBReconstructionProcess>())
        {
            foreach (var (_, requestEntity) in SystemAPI.Query<VOBYReconstructionRequest>().WithEntityAccess())
            {
                state.EntityManager.DestroyEntity(requestEntity);
            }
            return;
        }

        var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        // Only run if a request exists
        foreach (var (request, requestEntity) in SystemAPI.Query<VOBYReconstructionRequest>().WithEntityAccess())
        {
            // Use batchSize from the request component
            var processEntity = ecb.CreateEntity();
            ecb.AddComponent(processEntity, new VOBReconstructionProcess
            {
                Timer = 0f,
                NextAnimationIndex = 0,
                BatchSize = request.batchSize,
                BatchDelay = request.batchDelay, // 50ms delay between batches
                AnimationDuration = request.animationDuration, // Duration of the animation for each VOB
                FreezeUnbatchedVOBs = request.freezeUnbatchedVOBs,
                RandomizeVOBs = request.randomizeVOBs // Randomization control
            });

            ecb.DestroyEntity(requestEntity);
        }
    }
}