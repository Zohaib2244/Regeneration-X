using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

/// <summary>
/// Pulls loose VOBs toward any VOBY with an enabled VOBCollector, and absorbs them once they reach it:
/// the cube changes owner, drops its physics body, and flies into the owner's next free slot
/// (inside the shape if there's room, otherwise the spare-cube halo).
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct VOBCollectionSystem : ISystem
{
    struct CollectorData
    {
        public Entity Entity;
        public float4x4 Matrix;
        public float3 Position;
        public quaternion Rotation;
        public float RadiusSq;
        public float AbsorbDistance;
        public float AbsorbMargin;
        public float PullSpeed;
        public float AnimationDuration;
        public float CubeSize;
        public int NextFree;
        public int Capacity;
        public float OuterRadius;
        public bool Changed;
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<VOBCollector>();
        state.RequireForUpdate<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var collectors = new NativeList<CollectorData>(4, Allocator.Temp);
        foreach (var (collector, info, ltw, slots, entity) in SystemAPI
                     .Query<RefRO<VOBCollector>, RefRO<VOBYFormationInfo>, RefRO<LocalToWorld>, DynamicBuffer<VOBYSlot>>()
                     .WithEntityAccess())
        {
            if (!collector.ValueRO.Enabled) continue;
            collectors.Add(new CollectorData
            {
                Entity = entity,
                Matrix = ltw.ValueRO.Value,
                Position = ltw.ValueRO.Position,
                Rotation = ltw.ValueRO.Rotation,
                RadiusSq = collector.ValueRO.Radius * collector.ValueRO.Radius,
                AbsorbDistance = info.ValueRO.OuterRadius + collector.ValueRO.AbsorbMargin,
                AbsorbMargin = collector.ValueRO.AbsorbMargin,
                PullSpeed = collector.ValueRO.PullSpeed,
                AnimationDuration = math.max(0.01f, collector.ValueRO.AbsorbAnimationDuration),
                CubeSize = info.ValueRO.CubeSize,
                NextFree = info.ValueRO.NextFree,
                Capacity = slots.Length,
                OuterRadius = info.ValueRO.OuterRadius,
                Changed = false
            });
        }
        if (collectors.Length == 0)
        {
            collectors.Dispose();
            return;
        }

        var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        var slotLookup = SystemAPI.GetBufferLookup<VOBYSlot>(true);
        double now = SystemAPI.Time.ElapsedTime;
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (velocity, ltw, transform, vob, loose, entity) in SystemAPI
                     .Query<RefRW<PhysicsVelocity>, RefRO<LocalToWorld>, RefRO<LocalTransform>, RefRW<VOBComponent>, RefRO<VOBLoose>>()
                     .WithEntityAccess())
        {
            if (now < loose.ValueRO.PickupAfter) continue;

            float3 p = ltw.ValueRO.Position;

            // Nearest collector in range.
            int best = -1;
            float bestDistSq = float.MaxValue;
            for (int c = 0; c < collectors.Length; c++)
            {
                float d = math.distancesq(p, collectors[c].Position);
                if (d <= collectors[c].RadiusSq && d < bestDistSq)
                {
                    best = c;
                    bestDistSq = d;
                }
            }
            if (best < 0) continue;

            var col = collectors[best];
            float dist = math.sqrt(bestDistSq);

            if (dist > col.AbsorbDistance || col.NextFree >= col.Capacity)
            {
                // Pull in. Faster when close so cubes don't hang at the edge of the field.
                float3 dir = (col.Position - p) / math.max(dist, 1e-4f);
                float closeness = 1f - math.saturate(dist * dist / col.RadiusSq);
                float3 desired = dir * col.PullSpeed * (0.6f + closeness);
                velocity.ValueRW.Linear = math.lerp(velocity.ValueRO.Linear, desired, math.saturate(dt * 8f));
                continue;
            }

            // Absorb into the next free slot.
            var slots = slotLookup[col.Entity];
            int slotIndex = col.NextFree;
            VOBYSlot slot = slots[slotIndex];

            float4x4 inverse = math.inverse(col.Matrix);
            quaternion inverseRot = math.inverse(col.Rotation);

            ecb.RemoveComponent<VOBLoose>(entity);
            ecb.RemoveComponent<PhysicsVelocity>(entity);
            ecb.RemoveComponent<PhysicsMass>(entity);
            ecb.RemoveComponent<PhysicsGravityFactor>(entity);
            ecb.AddComponent(entity, new Parent { Value = col.Entity });
            ecb.SetComponent(entity, new LocalTransform
            {
                Position = math.transform(inverse, p),
                Rotation = math.mul(inverseRot, ltw.ValueRO.Rotation),
                Scale = transform.ValueRO.Scale
            });
            ecb.AddComponent(entity, new VOBDefaultReconstructionAnimation
            {
                StartPosition = p,
                StartRotation = ltw.ValueRO.Rotation,
                TargetPosition = slot.Position,
                TargetRotation = slot.Rotation,
                TargetIsLocal = true,
                AnimationTime = 0f,
                DelayTime = 0f,
                AnimationDuration = col.AnimationDuration,
                AnimationIndex = slotIndex
            });

            vob.ValueRW.VOBYParent = col.Entity;
            vob.ValueRW.Position = math.transform(col.Matrix, slot.Position);
            vob.ValueRW.Rotation = math.mul(col.Rotation, slot.Rotation);

            col.NextFree++;
            col.OuterRadius = math.max(col.OuterRadius, math.length(slot.Position) + col.CubeSize * 0.5f);
            col.AbsorbDistance = col.OuterRadius + col.AbsorbMargin;
            col.Changed = true;
            collectors[best] = col;
        }

        for (int c = 0; c < collectors.Length; c++)
        {
            if (!collectors[c].Changed) continue;
            var info = SystemAPI.GetComponentRW<VOBYFormationInfo>(collectors[c].Entity);
            info.ValueRW.NextFree = collectors[c].NextFree;
            info.ValueRW.OuterRadius = collectors[c].OuterRadius;
        }

        collectors.Dispose();
    }
}
