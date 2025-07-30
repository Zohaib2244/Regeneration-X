using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;

public partial class VSCALESystem : SystemBase
{
        private bool _enabled = false;
    protected override void OnUpdate()
    {
        // Handle requests
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (request, entity) in SystemAPI.Query<VSCALERequest>().WithEntityAccess())
        {
            _enabled = request.Enable;
            ecb.DestroyEntity(entity);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();

        if (!_enabled)
            return;

        float time = (float)SystemAPI.Time.ElapsedTime;
        // 1. Gather all VOB positions and entities for fast lookup
        var vobPositions = new NativeHashSet<int3>(1024, Allocator.TempJob);
        var vobEntities = new NativeList<Entity>(Allocator.TempJob);
        var vobEntityToPos = new NativeHashMap<Entity, int3>(1024, Allocator.TempJob);

        Entities.ForEach((Entity entity, in VOBComponent vob) =>
        {
            int3 pos = new int3(math.round(vob.Position));
            vobPositions.Add(pos);
            vobEntities.Add(entity);
            vobEntityToPos[entity] = pos;
        }).Run();

        // 2. Build a hash set of exterior VOB entities
        var exteriorVOBs = new NativeHashSet<Entity>(vobEntities.Length, Allocator.TempJob);

        foreach (var entity in vobEntities)
        {
            int3 pos = vobEntityToPos[entity];
            int3[] directions = new int3[]
            {
                new int3(1,0,0), new int3(-1,0,0),
                new int3(0,1,0), new int3(0,-1,0),
                new int3(0,0,1), new int3(0,0,-1)
            };
            foreach (var dir in directions)
            {
                if (!vobPositions.Contains(pos + dir))
                {
                    exteriorVOBs.Add(entity);
                    break;
                }
            }
        }

        // Copy exteriorVOBs to a readable hash set for parallel access
        var exteriorVOBsReadOnly = new NativeHashSet<Entity>(exteriorVOBs.Count, Allocator.TempJob);
        foreach (var entity in exteriorVOBs)
        {
            exteriorVOBsReadOnly.Add(entity);
        }
        
        vobPositions.Dispose();
        vobEntities.Dispose();
        vobEntityToPos.Dispose();
        exteriorVOBs.Dispose();

        Entities
        .WithReadOnly(exteriorVOBsReadOnly)
        .ForEach((Entity entity, ref LocalTransform localTransform, in VOBComponent vob) =>
        {
            // Use hash set lookup instead of linear search
            if (!exteriorVOBsReadOnly.Contains(entity))
                return;
        
            uint hash = (uint)(vob.VOBIndex * 73856093 ^ (int)vob.Position.x * 19349663 ^ (int)vob.Position.y * 83492791 ^ (int)vob.Position.z * 15485863);
            Random rand = new Random(hash);
        
            float minScale = rand.NextFloat(0.3f, 0.7f);
            float animOffset = rand.NextFloat(0f, 1.5f);
            float animDuration = 2.0f;
            float t = math.fmod(time + animOffset, animDuration) / animDuration;
            float scaleT = math.abs(2f * t - 1f);
            float scale = math.lerp(1.0f, minScale, scaleT);
        
            localTransform.Scale = scale;
        }).ScheduleParallel();
        
        exteriorVOBsReadOnly.Dispose();
    }
}