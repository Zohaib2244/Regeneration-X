using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using Unity.Collections;
using System.Linq;
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

        var processEntity = SystemAPI.GetSingletonEntity<VOBReconstructionProcess>();
        var process = SystemAPI.GetComponentRW<VOBReconstructionProcess>(processEntity);
        process.ValueRW.Timer += SystemAPI.Time.DeltaTime;

        if (process.ValueRO.Timer < process.ValueRO.BatchDelay)
        {
            return;
        }

        process.ValueRW.Timer = 0f;

        int animatedThisFrame = 0;
        int startIndex = process.ValueRO.NextAnimationIndex;

        // Gather all VOBs with required physics components - ADD LocalToWorld to query
        var vobQuery = SystemAPI.QueryBuilder()
            .WithAll<VOBComponent, LocalTransform, LocalToWorld, VOBExplodedTag>()
            .Build();

        var vobEntities = vobQuery.ToEntityArray(Allocator.Temp);
        var vobComponents = vobQuery.ToComponentDataArray<VOBComponent>(Allocator.Temp);
        var transforms = vobQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var localToWorlds = vobQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

        int[] sortedIndices;
        if (process.ValueRO.RandomizeVOBs)
        {
            // Use natural order (randomized/unsorted)
            sortedIndices = Enumerable.Range(0, vobEntities.Length).ToArray();
        }
        else
        {
            // Sort by VOBIndex
            sortedIndices = Enumerable.Range(0, vobEntities.Length)
                .OrderBy(i => vobComponents[i].VOBIndex)
                .ToArray();
        }
        // Keep track of which entities are being processed in this batch
        var processedEntities = new NativeHashSet<Entity>(process.ValueRO.BatchSize, Allocator.Temp);

        for (int batchIdx = 0; batchIdx < sortedIndices.Length; batchIdx++)
        {
            if (animatedThisFrame >= process.ValueRO.BatchSize)
                break;

            int i = sortedIndices[batchIdx];
            var entity = vobEntities[i];
            var vob = vobComponents[i];
            var transform = transforms[i];
            var localToWorld = localToWorlds[i];

            // Add to processed entities set
            processedEntities.Add(entity);

            // Store current world position before reparenting
            float3 currentWorldPos = localToWorld.Position;
            quaternion currentWorldRot = localToWorld.Rotation;
            float currentWorldScale = transform.Scale;

            // Remove physics components
            ecb.RemoveComponent<PhysicsVelocity>(entity);
            ecb.RemoveComponent<PhysicsMass>(entity);
            ecb.RemoveComponent<PhysicsGravityFactor>(entity);

            // Reparent to original VOBY
            ecb.AddComponent(entity, new Parent { Value = vob.VOBYParent });

            // Convert world position to local space relative to parent
            float3 localPos = currentWorldPos;
            quaternion localRot = currentWorldRot;

            // Get parent's LocalToWorld to convert world to local
            if (SystemAPI.HasComponent<LocalToWorld>(vob.VOBYParent))
            {
                var parentLTW = SystemAPI.GetComponentRO<LocalToWorld>(vob.VOBYParent);
                float4x4 parentInverse = math.inverse(parentLTW.ValueRO.Value);

                localPos = math.transform(parentInverse, currentWorldPos);
                localRot = math.mul(math.inverse(parentLTW.ValueRO.Rotation), currentWorldRot);
            }

            // Set LocalTransform to the calculated local position
            ecb.SetComponent(entity, new LocalTransform
            {
                Position = localPos,
                Rotation = localRot,
                Scale = currentWorldScale
            });

            switch (process.ValueRO.ReconstructionType)
            {
                case ReconstructionType.Default:
                    // Add default animation component
                    ecb.AddComponent(entity, new VOBDefaultReconstructionAnimation
                    {
                        StartPosition = currentWorldPos, // World position
                        StartRotation = currentWorldRot, // World rotation
                        TargetPosition = vob.Position,   // Target world position
                        TargetRotation = vob.Rotation,   // Target world rotation
                        AnimationTime = 0f,
                        DelayTime = 0f,
                        AnimationDuration = process.ValueRO.AnimationDuration, // Use the duration from the process
                        AnimationIndex = startIndex + animatedThisFrame
                    });
                    break;

                case ReconstructionType.Spiral:
                    // Add spiral animation component - still use world positions for interpolation
                    ecb.AddComponent(entity, new VOBSpiralReconstructionAnimation
                    {
                        StartPosition = currentWorldPos,
                        StartRotation = currentWorldRot,
                        TargetPosition = vob.Position,
                        TargetRotation = vob.Rotation,
                        SpiralCenter = process.ValueRO.Epicenter, // Assuming spiral center is at the target position
                        SpiralRadius = 4f,           // Example radius, adjust as needed
                        SpiralHeight = 6f,           // Example height, adjust as needed
                        SpiralRotations = 4f,        // Example rotations, adjust as needed
                        AnimationTime = 0f,
                        DelayTime = animatedThisFrame * 0.05f,
                        SpiralDuration = process.ValueRO.AnimationDuration * 2f, // Adjust duration for spiral phase
                        MoveDuration = process.ValueRO.AnimationDuration / 2f,   // Adjust duration for move phase
                        AnimationIndex = startIndex + animatedThisFrame
                    });
                    break;
            }
        // If FreezeUnbatchedVOBs is true, freeze all remaining VOBs
        if (process.ValueRO.FreezeUnbatchedVOBs)
        {
            for (int j = 0; j < vobEntities.Length; j++)
            {
                var _entity = vobEntities[j];

                // Skip entities that are already being processed in this batch
                if (processedEntities.Contains(_entity))
                    continue;

                // Remove physics components to freeze the VOB
                ecb.RemoveComponent<PhysicsVelocity>(_entity);
                ecb.RemoveComponent<PhysicsMass>(_entity);
                ecb.RemoveComponent<PhysicsGravityFactor>(_entity);
            }
        }
            ecb.RemoveComponent<VOBExplodedTag>(entity); // Remove exploded tag if exists

            animatedThisFrame++;
        }


        processedEntities.Dispose();
        vobEntities.Dispose();
        vobComponents.Dispose();
        transforms.Dispose();
        localToWorlds.Dispose();

        if (animatedThisFrame > 0)
        {
            process.ValueRW.NextAnimationIndex += animatedThisFrame;
        }
        else
        {
            ecb.DestroyEntity(processEntity);
        }
    }
}