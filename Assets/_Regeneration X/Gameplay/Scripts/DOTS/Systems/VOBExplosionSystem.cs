
using Unity.Entities;
using Unity.Physics;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Physics.Systems;



[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
[BurstCompile]
public partial struct VOBExplosionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        // Find all entities with ExplosionRequest
        foreach (var (explosion, explosionEntity) in SystemAPI.Query<ExplosionRequest>().WithEntityAccess())
        {
            // Find all VOBs (entities with VOBAuthoring/VOBComponent)
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>>().WithEntityAccess())
            {
                float3 position = transform.ValueRO.Position;
                float distance = math.distance(position, explosion.Epicenter);
                if (distance > explosion.Radius)
                    continue;

                // Calculate force direction and magnitude
                float3 direction = math.normalize(position - explosion.Epicenter);
                float forceAmount = explosion.Force * (1f - (distance / explosion.Radius));
                float3 velocity = direction * forceAmount;

                // Calculate angular velocity based on rotation amount
                float3 angularVelocity = direction * explosion.RotationAmount;

                // Add PhysicsMass (default mass = 1)
                var physicsMass = new PhysicsMass
                {
                    Transform = RigidTransform.identity,
                    InverseMass = 1f,
                    InverseInertia = new float3(1f),
                    AngularExpansionFactor = 0f
                };
                ecb.AddComponent(entity, physicsMass);
                ecb.AddComponent(entity, new PhysicsGravityFactor { Value = 1f });

                // Add PhysicsVelocity
                ecb.AddComponent(entity, new PhysicsVelocity
                {
                    Linear = velocity,
                    Angular = angularVelocity
                });
            }

            // Remove the request so it only happens once
            ecb.RemoveComponent<ExplosionRequest>(explosionEntity);
        }
    }
}