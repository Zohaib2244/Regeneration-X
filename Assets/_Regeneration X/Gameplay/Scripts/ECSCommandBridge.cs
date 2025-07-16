using AdvancedEditorTools.Attributes;
using UnityEngine;
using Unity.Entities;

public class ECSCommandBridge : MonoBehaviour
{
    // Reference to the ECS world
    private World ecsWorld;

    [Header("Explosion Parameters")]
    [BeginColumnArea(columnWidth: 0.5f)]
    public float radius = 5f;
    public float force = 10f;
    [EndColumnArea(includeLast = true)]
    public float rotationAmount = 1f;

    private void Awake()
    {
        // Initialize the ECS world
        ecsWorld = World.DefaultGameObjectInjectionWorld;
    }


    // Method to send an explosion request
    [Button("Request Explosion", 15)]
    public void RequestExplosion()
    {
        if (ecsWorld == null || !ecsWorld.IsCreated)
        {
            Debug.LogError("ECS World is not initialized or not created.");
            return;
        }
        // Create an explosion request entity
        var explosionEntity = ecsWorld.EntityManager.CreateEntity(typeof(ExplosionRequest));
        ecsWorld.EntityManager.SetComponentData(explosionEntity, new ExplosionRequest
        {
            Radius = radius,
            Force = force,
            RotationAmount = rotationAmount
        });
        Debug.Log($"Explosion requested with radius: {radius}, force: {force}, rotation amount: {rotationAmount}");
    }
    [Button("Request VOBY Reconstruction", 15)]
    public void RequestVOBYReconstruction()
    {
        if (ecsWorld == null || !ecsWorld.IsCreated)
        {
            Debug.LogError("ECS World is not initialized or not created.");
            return;
        }
        // Create a VOBY reconstruction request entity
       ecsWorld.EntityManager.CreateEntity(typeof(VOBYReconstructionRequest));
        Debug.Log("VOBY reconstruction requested.");
    }
}