using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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

        SetVOBSIndex(ref state);
        // Only run if a request exists
        foreach (var (request, requestEntity) in SystemAPI.Query<VOBYReconstructionRequest>().WithEntityAccess())
        {
            // Use batchSize from the request component
            var processEntity = ecb.CreateEntity();
            ecb.AddComponent(processEntity, new VOBReconstructionProcess
            {
                Epicenter = request.epicenter, // Set epicenter from request
                Timer = 0f,
                NextAnimationIndex = 0,
                BatchSize = request.batchSize,
                BatchDelay = request.batchDelay, // 50ms delay between batches
                AnimationDuration = request.animationDuration, // Duration of the animation for each VOB
                FreezeUnbatchedVOBs = request.freezeUnbatchedVOBs,
                RandomizeVOBs = request.randomizeVOBs, // Randomization control
                ReconstructionType = request.reconstructionType // Type of reconstruction
            });

            ecb.DestroyEntity(requestEntity);
        }
    }
    [BurstCompile]
    public void SetVOBSIndex(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<VOBYReconstructionRequest>(out var request))
            return;

        float3 referencePoint = request.reconstructionPoint;

        var query = SystemAPI.QueryBuilder().WithAll<VOBComponent, LocalToWorld>().Build();
        int entityCount = query.CalculateEntityCount();

        var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
        var distances = new NativeArray<float>(entityCount, Allocator.Temp);
        int index = 0;
        foreach (var (vob, entity) in SystemAPI.Query<RefRO<VOBComponent>>().WithAll<LocalToWorld>().WithEntityAccess())
        {
            float3 originalPos = vob.ValueRO.Position;
            entities[index] = entity;
            distances[index] = math.distance(originalPos, referencePoint);
            index++;
        }
        // Quick sort implementation
        QuickSort(ref entities, ref distances, 0, entityCount - 1);

        // Assign indices: closest gets 1, next gets 2, ...
        for (int i = 0; i < entityCount; i++)
        {
            var vob = SystemAPI.GetComponentRW<VOBComponent>(entities[i]);
            vob.ValueRW.VOBIndex = i + 1;
        }

        entities.Dispose();
        distances.Dispose();
    }
    [BurstCompile]
    private static void QuickSort(ref NativeArray<Entity> entities, ref NativeArray<float> distances, int low, int high)
    {
        if (low < high)
        {
            int pivotIndex = Partition(ref entities, ref distances, low, high);
            QuickSort(ref entities, ref distances, low, pivotIndex - 1);
            QuickSort(ref entities, ref distances, pivotIndex + 1, high);
        }
    }

    [BurstCompile]
    private static int Partition(ref NativeArray<Entity> entities, ref NativeArray<float> distances, int low, int high)
    {
        float pivot = distances[high];
        int i = low - 1;

        for (int j = low; j < high; j++)
        {
            if (distances[j] <= pivot)
            {
                i++;
                // Swap distances
                float tempDist = distances[i];
                distances[i] = distances[j];
                distances[j] = tempDist;

                // Swap entities
                Entity tempEntity = entities[i];
                entities[i] = entities[j];
                entities[j] = tempEntity;
            }
        }

        // Swap with pivot
        float tempDistPivot = distances[i + 1];
        distances[i + 1] = distances[high];
        distances[high] = tempDistPivot;

        Entity tempEntityPivot = entities[i + 1];
        entities[i + 1] = entities[high];
        entities[high] = tempEntityPivot;

        return i + 1;
    }
}