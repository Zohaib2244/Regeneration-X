using Unity.Entities;
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