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

        void Execute(ref LocalTransform transform, in VOBComponent vob, Entity entity, [EntityIndexInQuery] int entityInQueryIndex)
        {
            float3 worldPosition = transform.Position;
            quaternion worldRotation = transform.Rotation;

            float3 epicenter = VOBYEpicenterMap.TryGetValue(vob.VOBYParent, out var e) ? e : float3.zero;
            float distance = math.distance(worldPosition, epicenter);

            // Use ParallelWriter with entityInQueryIndex
            ECB.SetComponent(entityInQueryIndex, entity, new Parent { Value = Entity.Null });

            ECB.SetComponent(entityInQueryIndex, entity, new LocalTransform
            {
                Position = worldPosition,
                Rotation = worldRotation,
                Scale = transform.Scale
            });

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