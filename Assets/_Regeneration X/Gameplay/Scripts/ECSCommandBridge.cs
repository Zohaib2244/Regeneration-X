
using AdvancedEditorTools.Attributes;
using UnityEngine;
using Unity.Entities;

public class ECSCommandBridge : MonoBehaviour
{
    // Reference to the ECS world
    private World ecsWorld;

    [Header("Explosion Parameters")]
    [BeginColumnArea(columnWidth: 0.5f)]
    public Transform epicenter;
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
        // Create an explosion request entity
        var explosionEntity = ecsWorld.EntityManager.CreateEntity(typeof(ExplosionRequest));
        ecsWorld.EntityManager.SetComponentData(explosionEntity, new ExplosionRequest
        {
            Epicenter = epicenter.position,
            Radius = radius,
            Force = force,
            RotationAmount = rotationAmount
        });
    }
}