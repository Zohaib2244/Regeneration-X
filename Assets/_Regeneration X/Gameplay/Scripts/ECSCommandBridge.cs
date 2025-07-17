using AdvancedEditorTools.Attributes;
using UnityEngine;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
enum VOBYState
{
    Idle,
    Exploded,
    Reconstructed
}
public class ECSCommandBridge : MonoBehaviour
{
    // Reference to the ECS world
    private World ecsWorld;


    [Header("Explosion Parameters")]
    [BeginColumnArea(columnWidth: 0.5f)]

    [Tooltip("Transform representing the epicenter of the explosion.")]
    public Transform epicenter; // Epicenter of the explosion
    
    [Tooltip("Radius of the explosion effect.")]
    public float radius = 5f;
    
    [Tooltip("Force applied to VOBs during explosion.")]
    public float force = 10f;
    
    [Tooltip("Amount of rotational force applied to VOBs during explosion.")]
    public float rotationAmount = 1f;
    
    [Header("VOBY Reconstruction")]
    [NewColumn(columnWidth: 0.5f)]
    [Tooltip("If true, Exploded VOBs will be frozen in place.")]
    public bool freezeUnbatchedVOBs = false;
    
    [Tooltip("Delay in seconds between processing each batch of VOBs.")]
    public float batchDelay = 0.05f; // Delay between batches in seconds
    
    [Tooltip("Duration in seconds for each VOB Batch reconstruction animation.")]
    public float animationDuration = 0.25f; // Duration of the animation for each VOB
    
    [EndColumnArea(includeLast = true)]
    [Tooltip("Number of VOBs to process in each reconstruction batch.")]
    public int batchSize = 50; // Size of
    VOBYState currentState = VOBYState.Reconstructed;
    private void Awake()
    {
        // Initialize the ECS world
        ecsWorld = World.DefaultGameObjectInjectionWorld;
    }


    // Method to send an explosion request
    [Button("Explosive Protocol", 15)]
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
    [Button("Entropic Reconstitution Sequence", 15)]
    public void RequestVOBYReconstruction()
    {
        if(currentState != VOBYState.Exploded) return;
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
            freezeUnbatchedVOBs = freezeUnbatchedVOBs,
            batchSize = batchSize,
            batchDelay = batchDelay,
            animationDuration = animationDuration
        });
        Debug.Log("VOBY reconstruction requested.");
    }
}