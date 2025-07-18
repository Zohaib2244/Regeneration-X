using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using AdvancedEditorTools.Attributes;
enum VOBYState
{
    Idle,
    Exploded,
    Reconstructed,
    Magnetic
}
public class ECSCommandBridge : MonoBehaviour
{
    // Reference to the ECS world
    private World ecsWorld;


    [Header("Explosion Parameters")]

    [Tooltip("Transform representing the epicenter of the explosion.")]
    public Transform epicenter; // Epicenter of the explosion

    [Tooltip("Radius of the explosion effect.")]
    public float radius = 5f;

    [Tooltip("Force applied to VOBs during explosion.")]
    public float force = 10f;

    [Tooltip("Amount of rotational force applied to VOBs during explosion.")]
    public float rotationAmount = 1f;

    [Header("VOBY Reconstruction")]
    [Tooltip("If true, Exploded VOBs will be frozen in place.")]
    public bool freezeUnbatchedVOBs = false;
    [Tooltip("If true, VOBs will be randomized during reconstruction.")]
    public bool randomizeVOBs = false; // New field to control randomization of VOBs
    [Tooltip("Delay in seconds between processing each batch of VOBs.")]
    public float batchDelay = 0.05f; // Delay between batches in seconds

    [Tooltip("Duration in seconds for each VOB Batch reconstruction animation.")]
    public float animationDuration = 0.25f; // Duration of the animation for each VOB

    [Tooltip("Number of VOBs to process in each reconstruction batch.")]
    public int batchSize = 50; // Size of
    [Header("Magnetic Mode")]
    public Transform magnetTransform;

    public float magnetRadius = 10f;

    public float magnetForce = 15f;


    public float magnetConeAngle = 60f;
    VOBYState currentState = VOBYState.Reconstructed;

    private void Awake()
    {
        // Initialize the ECS world
        ecsWorld = World.DefaultGameObjectInjectionWorld;
    }
    private void Update()
    {
        // Sync magnet position with DOTS system when in magnetic mode
        if (currentState == VOBYState.Magnetic && magnetTransform != null)
        {
            UpdateMagnetPosition();
        }
    }

    [Button("Request Explosion")]
    // Method to send an explosion request
    public void RequestExplosion()
    {
        if (currentState != VOBYState.Reconstructed) return;
        if (ecsWorld == null || !ecsWorld.IsCreated)
        {
            Debug.LogError("ECS World is not initialized or not created.");
            return;
        }
        currentState = VOBYState.Exploded;
        // Create an explosion request entity
        var explosionEntity = ecsWorld.EntityManager.CreateEntity(typeof(ExplosionRequest));
        ecsWorld.EntityManager.SetComponentData(explosionEntity, new ExplosionRequest
        {
            Epicenter = epicenter.position,
            Radius = radius,
            Force = force,
            RotationAmount = rotationAmount
        });
        Debug.Log($"Explosion requested with radius: {radius}, force: {force}, rotation amount: {rotationAmount}");
    }
    [Button("Request VOBY Reconstruction")]
    public void RequestVOBYReconstruction()
    {
        if (currentState != VOBYState.Exploded) return;
        currentState = VOBYState.Reconstructed;
        if (ecsWorld == null || !ecsWorld.IsCreated)
        {
            Debug.LogError("ECS World is not initialized or not created.");
            return;
        }
        // Create a VOBY reconstruction request entity
        var requestEntity = ecsWorld.EntityManager.CreateEntity(typeof(VOBYReconstructionRequest));
        ecsWorld.EntityManager.SetComponentData(requestEntity, new VOBYReconstructionRequest
        {
            randomizeVOBs = randomizeVOBs,
            freezeUnbatchedVOBs = freezeUnbatchedVOBs,
            batchSize = batchSize,
            batchDelay = batchDelay,
            animationDuration = animationDuration
        });
        Debug.Log("VOBY reconstruction requested.");
    }
    #region Manetism mode
    private void UpdateMagnetPosition()
    {
        if (ecsWorld == null || !ecsWorld.IsCreated) return;

        var requestEntity = ecsWorld.EntityManager.CreateEntity(typeof(MagneticRequest));
        ecsWorld.EntityManager.SetComponentData(requestEntity, new MagneticRequest
        {
            MagnetPosition = magnetTransform.position,
            MagnetDirection = magnetTransform.forward,
            Radius = magnetRadius,
            Force = magnetForce,
            ConeAngle = math.radians(magnetConeAngle)
        });
    }
    [Button("Request Magnetic Mode")]
    public void RequestMagneticMode()
    {
        if (currentState == VOBYState.Magnetic) return;

        currentState = VOBYState.Magnetic;

        if (ecsWorld == null || !ecsWorld.IsCreated)
        {
            Debug.LogError("ECS World is not initialized or not created.");
            return;
        }

        Debug.Log("Magnetic mode activated.");
    }
    [Button("Deactivate Magnetic Field")]
    public void DeactivateMagneticField()
    {
        if (currentState != VOBYState.Magnetic) return;

        currentState = VOBYState.Exploded;

        // Deactivate magnetic field
        var query = ecsWorld.EntityManager.CreateEntityQuery(typeof(MagneticFieldComponent));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var entity in entities)
        {
            var field = ecsWorld.EntityManager.GetComponentData<MagneticFieldComponent>(entity);
            field.IsActive = false;
            ecsWorld.EntityManager.SetComponentData(entity, field);
        }

    entities.Dispose();
        Debug.Log("Magnetic field deactivated.");
    }
    #endregion
}