using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;
// Helper struct for sorting VOBs by distance
public struct VOBDistanceData
{
    public Entity Entity;
    public float Distance;
    public float3 WorldPosition;
    public float3 OriginalPosition;
    public bool IsParented;
}

public partial class MagneticSystem : SystemBase
{
    private EntityQuery magneticFieldQuery;
    private EntityQuery vobQuery;
    private EntityQuery magneticRequestQuery;

    protected override void OnCreate()
    {
        magneticFieldQuery = GetEntityQuery(typeof(MagneticFieldComponent));
        vobQuery = GetEntityQuery(typeof(VOBComponent), typeof(LocalTransform));
        magneticRequestQuery = GetEntityQuery(typeof(MagneticRequest));
    }

    protected override void OnUpdate()
    {
        // Process magnetic requests first
        ProcessMagneticRequests();

        // Apply magnetic forces to VOBs
        ApplyMagneticForces();
    }

    private void ProcessMagneticRequests()
    {
        var requests = magneticRequestQuery.ToComponentDataArray<MagneticRequest>(Allocator.Temp);

        if (requests.Length == 0) return;

        var magneticFields = magneticFieldQuery.ToEntityArray(Allocator.Temp);

        foreach (var request in requests)
        {
            if (magneticFields.Length > 0)
            {
                // Update existing magnetic field
                var magneticField = SystemAPI.GetComponentRW<MagneticFieldComponent>(magneticFields[0]);
                magneticField.ValueRW.Position = request.MagnetPosition;
                magneticField.ValueRW.Direction = request.MagnetDirection;
                magneticField.ValueRW.Radius = request.Radius;
                magneticField.ValueRW.Force = request.Force;
                magneticField.ValueRW.ConeAngle = request.ConeAngle;
                magneticField.ValueRW.IsActive = true;
            }
            else
            {
                // Create new magnetic field
                var magneticEntity = EntityManager.CreateEntity(typeof(MagneticFieldComponent));
                EntityManager.SetComponentData(magneticEntity, new MagneticFieldComponent
                {
                    Position = request.MagnetPosition,
                    Direction = request.MagnetDirection,
                    Radius = request.Radius,
                    Force = request.Force,
                    ConeAngle = request.ConeAngle,
                    IsActive = true
                });
            }
        }

        // Clean up requests
        EntityManager.DestroyEntity(magneticRequestQuery);
        requests.Dispose();
        magneticFields.Dispose();
    }

    private void ApplyMagneticForces()
    {
        var magneticFields = magneticFieldQuery.ToComponentDataArray<MagneticFieldComponent>(Allocator.Temp);
        var deltaTime = SystemAPI.Time.DeltaTime;

        if (magneticFields.Length == 0 || !magneticFields[0].IsActive)
        {
            // No magnetic field active - handle return to original positions
            HandleReturnToOriginalPositions(deltaTime);
            magneticFields.Dispose();
            return;
        }

        var magneticField = magneticFields[0];

        // Create an EntityCommandBuffer to queue structural changes
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // First pass: Collect all VOBs within magnetic influence and sort by distance
        var vobEntities = vobQuery.ToEntityArray(Allocator.Temp);
        var vobData = new NativeList<VOBDistanceData>(Allocator.Temp);

        for (int i = 0; i < vobEntities.Length; i++)
        {
            var entity = vobEntities[i];

            // Skip VOBs with physics
            if (SystemAPI.HasComponent<PhysicsVelocity>(entity))
                continue;

            var transform = SystemAPI.GetComponent<LocalTransform>(entity);
            bool isParented = SystemAPI.HasComponent<Parent>(entity);

            // Get world position
            float3 worldPosition = transform.Position;
            if (isParented)
            {
                worldPosition = SystemAPI.GetComponent<LocalToWorld>(entity).Position;
            }

            // Calculate distance to magnet
            var toMagnet = magneticField.Position - worldPosition;
            var distance = math.length(toMagnet);

            // Check if within magnetic influence
            bool withinRange = distance <= magneticField.Radius && distance >= 0.1f;
            bool withinCone = true;

            if (withinRange)
            {
                var directionToMagnet = toMagnet / distance;
                var angleToMagnet = math.acos(math.clamp(math.dot(magneticField.Direction, directionToMagnet), -1f, 1f));
                withinCone = angleToMagnet <= magneticField.ConeAngle / 2f;
            }

            if (withinRange && withinCone)
            {
                vobData.Add(new VOBDistanceData
                {
                    Entity = entity,
                    Distance = distance,
                    WorldPosition = worldPosition,
                    IsParented = isParented,
                    OriginalPosition = SystemAPI.GetComponent<VOBComponent>(entity).Position
                });
            }
        }

        // Sort VOBs by distance (closest first)
        if (vobData.Length > 0)
        {
            // Simple bubble sort
            for (int i = 0; i < vobData.Length - 1; i++)
            {
                for (int j = 0; j < vobData.Length - i - 1; j++)
                {
                    if (vobData[j].Distance > vobData[j + 1].Distance)
                    {
                        var temp = vobData[j];
                        vobData[j] = vobData[j + 1];
                        vobData[j + 1] = temp;
                    }
                }
            }

            // Apply curved hill effect
            for (int i = 0; i < vobData.Length; i++)
            {
                var vobInfo = vobData[i];
                var entity = vobInfo.Entity;
                var transform = SystemAPI.GetComponentRW<LocalTransform>(entity);
                var magneticForce = SystemAPI.GetComponentRW<VOBMagneticForce>(entity);

                float3 targetPosition = CalculateCurvedHillPosition(i, vobData.Length, magneticField.Position, vobInfo.OriginalPosition, vobInfo.Distance);

                // Calculate force towards target position
                var toTarget = targetPosition - vobInfo.WorldPosition;
                var distanceToTarget = math.length(toTarget);

                if (distanceToTarget > 0.1f)
                {
                    var directionToTarget = toTarget / distanceToTarget;

                    // Adjust force based on position in curve
                    var baseForce = magneticField.Force;
                    var positionRatio = (float)i / math.max(1, vobData.Length - 1);
                    var curveForceMultiplier = 1.0f - (positionRatio * 0.3f); // Reduce force for VOBs further back

                    var forceMagnitude = baseForce * curveForceMultiplier * math.min(1f, distanceToTarget / 2f);
                    var forceVector = directionToTarget * forceMagnitude;

                    // Update magnetic force component
                    magneticForce.ValueRW.Force = forceVector;
                    magneticForce.ValueRW.Velocity = math.lerp(magneticForce.ValueRW.Velocity, forceVector, deltaTime * 5f);

                    // Clear any return animation
                    if (SystemAPI.HasComponent<VOBDefaultReconstructionAnimation>(entity))
                    {
                        ecb.RemoveComponent<VOBDefaultReconstructionAnimation>(entity);
                    }

                    // Apply movement
                    ApplyMagneticMovement(entity, ref transform.ValueRW, magneticForce.ValueRW, vobInfo.IsParented, deltaTime);
                }
            }
        }

        // Handle VOBs outside magnetic influence
        Entities
            .WithAll<VOBComponent>()
            .WithNone<PhysicsVelocity>()
            .ForEach((Entity entity, ref LocalTransform transform, ref VOBMagneticForce magneticForce) =>
            {
                bool isParented = SystemAPI.HasComponent<Parent>(entity);
                float3 worldPosition = transform.Position;
                if (isParented)
                {
                    worldPosition = SystemAPI.GetComponent<LocalToWorld>(entity).Position;
                }

                var toMagnet = magneticField.Position - worldPosition;
                var distance = math.length(toMagnet);

                bool withinRange = distance <= magneticField.Radius && distance >= 0.1f;
                bool withinCone = true;

                if (withinRange)
                {
                    var directionToMagnet = toMagnet / distance;
                    var angleToMagnet = math.acos(math.clamp(math.dot(magneticField.Direction, directionToMagnet), -1f, 1f));
                    withinCone = angleToMagnet <= magneticField.ConeAngle / 2f;
                }

                if (!withinRange || !withinCone)
                {
                    HandleOutOfRange(entity, ref transform, ref magneticForce, isParented, deltaTime, ecb);
                }
            }).WithoutBurst().Run();

        ecb.Playback(EntityManager);
        ecb.Dispose();

        vobEntities.Dispose();
        vobData.Dispose();
        magneticFields.Dispose();
    }

    private float3 CalculateCurvedHillPosition(int index, int totalVOBs, float3 magnetPosition, float3 originalPosition, float currentDistance)
    {
        if (index == 0)
        {
            // First VOB (closest) goes to magnet
            return magnetPosition;
        }

        // Calculate curve parameters
        float positionRatio = (float)index / math.max(1, totalVOBs - 1);

        // Create a curved path from original position to magnet
        float3 directionToMagnet = math.normalize(magnetPosition - originalPosition);
        float3 perpendicular = math.cross(directionToMagnet, new float3(0, 1, 0));

        // If perpendicular is too small, use a different axis
        if (math.length(perpendicular) < 0.1f)
        {
            perpendicular = math.cross(directionToMagnet, new float3(1, 0, 0));
        }
        perpendicular = math.normalize(perpendicular);

        // Calculate base position along the path
        float3 basePosition = math.lerp(originalPosition, magnetPosition, 1.0f - positionRatio);

        // Add curve height (parabolic curve)
        float curveHeight = 4.0f * positionRatio * (1.0f - positionRatio); // Parabolic curve
        float maxHeight = 3.0f; // Maximum height of the curve

        // Add upward curve
        float3 upwardOffset = new float3(0, curveHeight * maxHeight, 0);

        // Add slight lateral spread for more natural look
        float lateralSpread = math.sin(positionRatio * math.PI) * 1.5f;
        float3 lateralOffset = perpendicular * lateralSpread;

        // Add some randomness based on index for more organic look
        float randomOffset = math.sin(index * 2.3f) * 0.5f;
        float3 randomLateral = perpendicular * randomOffset;

        return basePosition + upwardOffset + lateralOffset + randomLateral;
    }

    // ...existing code...

    private void HandleReturnToOriginalPositions(float deltaTime)
    {
        // Only handle return animation if there are no active magnetic fields
        // This prevents interfering with normal reconstruction
        if (magneticFieldQuery.CalculateEntityCount() == 0)
        {
            return; // No magnetic fields exist, don't interfere with normal reconstruction
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Handle return animation for all VOBs when magnetism is off (excluding physics VOBs)
        Entities
            .WithAll<VOBComponent>()
            //.WithNone<PhysicsVelocity>() // Exclude VOBs with physics - removed due to error
            .ForEach((Entity entity, ref LocalTransform transform, ref VOBMagneticForce magneticForce) =>
            {
                bool isParented = SystemAPI.HasComponent<Parent>(entity);

                HandleOutOfRange(entity, ref transform, ref magneticForce, isParented, deltaTime, ecb);
            }).WithoutBurst().Run();

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private void HandleOutOfRange(Entity entity, ref LocalTransform transform, ref VOBMagneticForce magneticForce, bool isParented, float deltaTime, EntityCommandBuffer ecb)
    {
        // Clear magnetic forces
        magneticForce.Force = float3.zero;
        magneticForce.Velocity = math.lerp(magneticForce.Velocity, float3.zero, deltaTime * 2f);

        // Return to original position for non-physics entities
        if (SystemAPI.HasComponent<VOBComponent>(entity))
        {
            var vobComponent = SystemAPI.GetComponent<VOBComponent>(entity);
            var originalPosition = vobComponent.Position;
            var originalRotation = vobComponent.Rotation;

            // Check if we need to start a return animation using your existing system
            if (!SystemAPI.HasComponent<VOBDefaultReconstructionAnimation>(entity))
            {
                // Calculate current world position for distance check
                float3 currentWorldPos = transform.Position;
                if (isParented)
                {
                    currentWorldPos = SystemAPI.GetComponent<LocalToWorld>(entity).Position;
                }

                // Only start return animation if we're significantly displaced
                if (math.distance(currentWorldPos, originalPosition) > 0.1f)
                {
                    // Use EntityCommandBuffer to add component
                    ecb.AddComponent(entity, new VOBDefaultReconstructionAnimation
                    {
                        StartPosition = currentWorldPos,
                        StartRotation = transform.Rotation,
                        TargetPosition = originalPosition,
                        TargetRotation = originalRotation,
                        AnimationTime = 0f,
                        DelayTime = 0f, // No delay for return animation
                        AnimationDuration = 1.5f, // Adjust duration as needed
                        AnimationIndex = 0 // Not used for return animation
                    });
                }
            }
        }
    }

    private void ApplyMagneticMovement(Entity entity, ref LocalTransform transform, VOBMagneticForce magneticForce, bool isParented, float deltaTime)
    {
        if (isParented)
        {
            // Parented entities: Convert world space movement to local space
            var parent = SystemAPI.GetComponent<Parent>(entity);

            if (SystemAPI.HasComponent<LocalToWorld>(parent.Value))
            {
                var parentLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(parent.Value);

                // Convert world space velocity to local space
                var worldMovement = magneticForce.Velocity * deltaTime;
                var localMovement = math.mul(math.inverse(parentLocalToWorld.Rotation), worldMovement);

                transform.Position += localMovement;
            }
        }
        else
        {
            // Unparented without physics: Direct position update
            transform.Position += magneticForce.Velocity * deltaTime;
        }
    }
}