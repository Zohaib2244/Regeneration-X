using Unity.Entities;
using Unity.Physics;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
[BurstCompile]
public partial struct VOBExplosionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        var vobyEpicenterMap = new NativeHashMap<Entity, float3>(64, state.WorldUnmanaged.UpdateAllocator.ToAllocator);
        foreach (var (voby, vobyEntity) in SystemAPI.Query<RefRO<VOBYComponent>>().WithEntityAccess())
        {
            vobyEpicenterMap.Add(vobyEntity, voby.ValueRO.epicenter);
        }

        JobHandle jobHandle = state.Dependency;

        foreach (var (explosion, explosionEntity) in SystemAPI.Query<ExplosionRequest>().WithEntityAccess())
        {
            var explosionData = explosion;

            // Schedule the job and assign to jobHandle
            jobHandle = new VOBExplosionJob
            {
                ECB = ecb.AsParallelWriter(),
                VOBYEpicenterMap = vobyEpicenterMap,
                Explosion = explosionData
            }.ScheduleParallel(jobHandle);

            // Do NOT call ecb.RemoveComponent here!
        }

        // Complete the job before using ECB directly
        jobHandle.Complete();

        foreach (var (explosion, explosionEntity) in SystemAPI.Query<ExplosionRequest>().WithEntityAccess())
        {
            ecb.RemoveComponent<ExplosionRequest>(explosionEntity);
        }

        state.Dependency = jobHandle;

        vobyEpicenterMap.Dispose();
    }

     [BurstCompile]
    public partial struct VOBExplosionJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly] public NativeHashMap<Entity, float3> VOBYEpicenterMap;
        public ExplosionRequest Explosion;
    
        void Execute(ref LocalTransform transform, in LocalToWorld localToWorld, in VOBComponent vob, Entity entity, [EntityIndexInQuery] int entityInQueryIndex)
        {
            // Use LocalToWorld for accurate world position
            float3 worldPosition = localToWorld.Position;
            quaternion worldRotation = localToWorld.Rotation;
            float worldScale = transform.Scale;
    
            float3 epicenter = VOBYEpicenterMap.TryGetValue(vob.VOBYParent, out var e) ? e : float3.zero;
            float distance = math.distance(worldPosition, epicenter);
    
            // Unparent (set to null parent)
            ECB.SetComponent(entityInQueryIndex, entity, new Parent { Value = Entity.Null });
    
            // IMPORTANT: Set the transform to maintain world position after unparenting
            ECB.SetComponent(entityInQueryIndex, entity, new LocalTransform
            {
                Position = worldPosition,    // Keep same world position
                Rotation = worldRotation,    // Keep same world rotation
                Scale = worldScale           // Keep same world scale
            });
    
            // Calculate and apply physics
            float3 direction = math.normalize(worldPosition - epicenter);
            float forceAmount = Explosion.Force * (1f - (distance / Explosion.Radius));
            float3 velocity = direction * forceAmount;
            float3 angularVelocity = direction * Explosion.RotationAmount;
    
            var physicsMass = new PhysicsMass
            {
                Transform = RigidTransform.identity,
                InverseMass = 1f,
                InverseInertia = new float3(1f),
                AngularExpansionFactor = 0f
            };
            ECB.AddComponent(entityInQueryIndex, entity, physicsMass);
            ECB.AddComponent(entityInQueryIndex, entity, new PhysicsGravityFactor { Value = 1f });
            ECB.AddComponent(entityInQueryIndex, entity, new PhysicsVelocity
            {
                Linear = velocity,
                Angular = angularVelocity
            });
        }
    }
}