using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

/// <summary>Counts how many (non-loose) cubes each VOBY owns and writes it to VOBYCubeCount.</summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct VOBYCubeCountSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<VOBYCubeCount>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var counts = new NativeHashMap<Entity, int>(16, Allocator.Temp);

        foreach (var vob in SystemAPI.Query<RefRO<VOBComponent>>().WithNone<VOBLoose>())
        {
            Entity owner = vob.ValueRO.VOBYParent;
            if (owner == Entity.Null) continue;
            counts.TryGetValue(owner, out int c);
            counts[owner] = c + 1;
        }

        foreach (var (count, entity) in SystemAPI.Query<RefRW<VOBYCubeCount>>().WithEntityAccess())
        {
            counts.TryGetValue(entity, out int c);
            count.ValueRW.Value = c;
        }

        counts.Dispose();
    }
}
