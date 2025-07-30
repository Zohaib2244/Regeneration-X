using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class VAVEPulsateSystem : SystemBase
{
    private float pulseTimer = 0f;
    private bool isActive = false;
    private float strength = 1f;
    private float duration = 1f;
    private float3 center = float3.zero;
    private bool resetPositions = false;

    protected override void OnUpdate()
    {
        // Check for new requests
        Entities
            .WithStructuralChanges()
            .WithoutBurst()
            .ForEach((Entity entity, in VOBYPulsateRequest request) =>
        {
            if (!request.IsActive && isActive)
            {
                resetPositions = true;
            }

            isActive = request.IsActive;
            strength = request.Strength;
            duration = request.Duration;
            // Convert world center to local space if needed
            center = request.Center; // This should be in parent's local space (usually float3.zero)
            pulseTimer = 0f;
            EntityManager.DestroyEntity(entity);
        }).Run();

        if (resetPositions)
        {
            Entities.ForEach((ref LocalTransform localTransform, in VOBComponent vob, in Parent parent, in LocalToWorld parentLocalToWorld) =>
            {
                float4x4 parentWorldToLocal = math.inverse(parentLocalToWorld.Value);
                float3 localVobPosition = math.transform(parentWorldToLocal, vob.Position);

                localTransform.Position = localVobPosition;
            }).Schedule();
            resetPositions = false;
            return;
        }

        if (!isActive) return;

        pulseTimer += SystemAPI.Time.DeltaTime;

        // Keep looping the pulse infinitely
        float normalizedTime = (pulseTimer % duration) / duration;
        float t = math.sin(normalizedTime * math.PI * 2f); // Continuous sine wave

        // Capture required fields as locals to avoid capturing 'this'
        float capturedStrength = strength;
        float capturedT = t;
        float3 capturedCenter = center;

              Entities
            .ForEach((ref LocalTransform localTransform, in VOBComponent vob, in Parent parent, in LocalToWorld parentLocalToWorld) =>
            {
                float4x4 parentWorldToLocal = math.inverse(parentLocalToWorld.Value);
                float3 localCenter = math.transform(parentWorldToLocal, capturedCenter);
                float3 originalLocalPosition = math.transform(parentWorldToLocal, vob.Position);
        
                float3 dir = math.normalizesafe(vob.Position - localCenter);
                float3 newPosition = vob.Position + dir * capturedStrength * capturedT;
        
                localTransform.Position = newPosition;
            })
            .Schedule();
    }
}