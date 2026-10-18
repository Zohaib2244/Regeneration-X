using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using AdvancedEditorTools.Attributes;
using DG.Tweening;
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
    [BeginColumnArea(columnWidth: 0.45f)]
    [Tooltip("Transform representing the epicenter of the explosion.")]
    public Transform epicenter; // Epicenter of the explosion

    [Tooltip("Radius of the explosion effect.")]
    public float radius = 5f;

    [Tooltip("Force applied to VOBs during explosion.")]
    public float force = 10f;

    [Tooltip("Amount of rotational force applied to VOBs during explosion.")]
    public float rotationAmount = 1f;
    public float slowmoDuration = 0.5f; // Duration of the slow motion effect
    public float slowmoTransitionTime = 0.5f; // Transition time for the slow motion effect
    public float slowmoTargetTimeScale = 0.5f; // Target time scale during slow motion



    [Header("VOBY Reconstruction")]
    [NewColumn(columnWidth: 0.45f)]
    [Tooltip("Transform representing the point where VOBs will be reconstructed.")]
    public Transform reconstructionPoint; // Point where VOBs will be reconstructed
    [Tooltip("Shape of the VOBY to be reconstructed.")]
    public VOBYShape vobyShape = VOBYShape.Cube; // Shape of the VOBY to be reconstructed
    [Tooltip("Type of reconstruction animation to use.")]
    public ReconstructionType reconstructionType = ReconstructionType.Default; // Type of reconstruction (default or spiral)
    [Tooltip("If true, Exploded VOBs will be frozen in place.")]
    public bool freezeUnbatchedVOBs = false;
    [Tooltip("If true, VOBs will be randomized during reconstruction.")]
    public bool randomizeVOBs = false; // New field to control randomization of VOBs
    [Tooltip("Delay in seconds between processing each batch of VOBs.")]
    public float batchDelay = 0.05f; // Delay between batches in seconds

    [Tooltip("Duration in seconds for each VOB Batch reconstruction animation.")]
    public float animationDuration = 0.25f; // Duration of the animation for each VOB

    [Tooltip("Number of VOBs to process in each reconstruction batch.")]
    [EndColumnArea(includeLast = true)]
    public int batchSize = 50; // Size of



    [Header("Magnetic Mode")]
    [BeginColumnArea(columnWidth: 0.45f)]
    public bool magneticMode = false; // Flag to enable/disable magnetic mode
    [Tooltip("Transform representing the magnet's position and orientation.")]
    public Transform magnetTransform;
    [Tooltip("Radius of the magnetic field.")]
    public float magnetRadius = 10f;
    [Tooltip("Force applied to VOBs in the magnetic field.")]
    public float magnetForce = 15f;
    [Tooltip("Angle of the cone for the magnetic field in degrees.")]
    public float magnetConeAngle = 60f;

    [Header("VOBY Pulsate")]
    [NewColumn(columnWidth: 0.45f)]
    [Tooltip("Enable or disable the pulsate effect.")]
    public bool pulsateActive = false;
    [Tooltip("Strength of the pulsate effect.")]
    public float pulsateStrength = 2f;
    [Tooltip("Duration of the pulsate effect.")]
    public float pulsateDuration = 1f;
    [Tooltip("Center point for the pulsate effect.")]
    [EndColumnArea(includeLast = true)]
    public Transform pulsateCenter;


    [Header("VSCALE Mode")]
    [BeginColumnArea(columnWidth: 0.45f)]
    [Tooltip("Enable or disable VSCALE mode.")]
    [EndColumnArea(includeLast = true)]
    public bool vscaleEnabled = false; // Flag to enable/disable VSCALE mode


    [Header("Formations & Cube Mass")]
    [Tooltip("Shape used by Morph, and by reconstruction after an explosion. Leave empty to rebuild the old JSON shape.")]
    public VOBFormation targetFormation;
    [Tooltip("In Play mode, number keys 1-9 morph the player into these.")]
    public VOBFormation[] hotkeyFormations = new VOBFormation[0];
    [Tooltip("Optional: the player VOBY follows this transform (drive it with any character controller).")]
    public Transform playerAnchor;
    [Tooltip("Cubes knocked off by the X key / Drop Test Cubes button.")]
    public int detachTestCount = 50;
    public float detachForce = 6f;
    [Tooltip("Seconds before knocked-off cubes can be collected again.")]
    public float detachPickupDelay = 1.5f;
    public bool showDebugHUD = true;
    [Tooltip("Number keys 1-9 morph into Hotkey Formations. PlayerController turns this off and uses its own forms.")]
    public bool enableNumberHotkeys = true;

    Entity controlledVOBY = Entity.Null;
    string lastMessage;

    VOBYState currentState = VOBYState.Reconstructed;

    #region Essentials
    private void Awake()
    {
        // Initialize the ECS world
        ecsWorld = World.DefaultGameObjectInjectionWorld;
    }
    private void Update()
    {
        HandleFormationHotkeys();

        // Sync magnet position with DOTS system when in magnetic mode
        if (currentState == VOBYState.Magnetic && magnetTransform != null)
        {
            UpdateMagnetPosition();
        }
    }
    #endregion


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
        SoundManager.Instance.PlayExplosionSound(); // Play explosion sound
        DOVirtual.DelayedCall(slowmoDuration + slowmoTransitionTime + slowmoTransitionTime, () =>
        {
            SoundManager.Instance.PlayBlockFallSound();
        });
        NuttyUtilities.TriggerSlomo(slowmoDuration, slowmoTransitionTime, slowmoTargetTimeScale); // Trigger slow motion effect
        Debug.Log($"Explosion requested with radius: {radius}, force: {force}, rotation amount: {rotationAmount}");
    }
    [Button("Request VOBY Reconstruction")]
    public void RequestVOBYReconstruction()
    {
        if (currentState != VOBYState.Exploded) return;
        if (targetFormation != null)
        {
            // Rebuild into the chosen formation instead of the JSON shape.
            MorphTo(targetFormation);
            return;
        }
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
            reconstructionPoint = reconstructionPoint.position, // Set the reconstruction point
            VOBYShape = vobyShape, // Set the shape of the VOBY
            randomizeVOBs = randomizeVOBs,
            freezeUnbatchedVOBs = freezeUnbatchedVOBs,
            batchSize = batchSize,
            batchDelay = batchDelay,
            animationDuration = animationDuration,
            reconstructionType = reconstructionType // Set the type of reconstruction
        });
        SoundManager.Instance.PlayReconstructionSound(); // Play reconstruction sound
        Debug.Log("VOBY reconstruction requested.");
    }
    [Button("Toggle VOBY Pulsate")]
    public void RequestVOBYPulsate()
    {
        if (ecsWorld == null || !ecsWorld.IsCreated)
        {
            Debug.LogError("ECS World is not initialized or not created.");
            return;
        }
        var entity = ecsWorld.EntityManager.CreateEntity(typeof(VOBYPulsateRequest));
        ecsWorld.EntityManager.SetComponentData(entity, new VOBYPulsateRequest
        {
            IsActive = pulsateActive,
            Strength = pulsateStrength,
            Duration = pulsateDuration,
            Center = pulsateCenter != null ? (float3)pulsateCenter.position : float3.zero
        });
        Debug.Log($"VOBY Pulsate requested: {(pulsateActive ? "On" : "Off")}, Strength: {pulsateStrength}, Duration: {pulsateDuration}");
    }
    [Button("Toggle VSCALE Mode")]
    public void RequestVSCALE()
    {
        if (ecsWorld == null || !ecsWorld.IsCreated)
        {
            Debug.LogError("ECS World is not initialized or not created.");
            return;
        }
        var entity = ecsWorld.EntityManager.CreateEntity(typeof(VSCALERequest));
        ecsWorld.EntityManager.SetComponentData(entity, new VSCALERequest
        {
            Enable = vscaleEnabled
        });
        Debug.Log($"VSCALE mode {(vscaleEnabled ? "enabled" : "disabled")}");
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
    [Button("Toggle Magnetic Mode")]
    public void ToggleMagneticMode()
    {
        if (ecsWorld == null || !ecsWorld.IsCreated)
        {
            Debug.LogError("ECS World is not initialized or not created.");
            return;
        }

        if (magneticMode)
        {
            // Activate magnetic mode
            currentState = VOBYState.Magnetic;
            Debug.Log("Magnetic mode activated.");

            // Optionally, send a MagneticRequest to update the field
            if (magnetTransform != null)
            {
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
        }
        else
        {
            // Deactivate magnetic mode
            if (currentState == VOBYState.Magnetic)
                currentState = VOBYState.Exploded;

            // Deactivate all magnetic fields
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
    }
    #endregion

    #region Formations & cube mass
    [Button("Morph To Target Formation")]
    public void RequestMorph() => MorphTo(targetFormation);

    /// <summary>Morphs the player (or the only VOBY) into <paramref name="formation"/> if it owns enough cubes.</summary>
    public MorphResult MorphTo(VOBFormation formation)
    {
        if (!WorldReady()) return MorphResult.Invalid;
        if (formation == null)
        {
            Debug.LogWarning("No formation assigned.");
            return MorphResult.Invalid;
        }
        if (currentState == VOBYState.Magnetic)
        {
            lastMessage = "Turn off magnetic mode before morphing.";
            Debug.LogWarning(lastMessage);
            return MorphResult.Busy;
        }

        var em = ecsWorld.EntityManager;
        Entity owner = GetControlledVOBY();
        MorphResult result = FormationMorphUtility.PrepareMorph(em, owner, formation, out int missing);
        switch (result)
        {
            case MorphResult.NotEnoughCubes:
                lastMessage = $"Need {missing} more cubes for {formation.name} (costs {formation.Cost}).";
                Debug.Log(lastMessage);
                return result;
            case MorphResult.Busy:
                lastMessage = "Still finishing the last morph.";
                return result;
            case MorphResult.Invalid:
                lastMessage = "No VOBY to morph.";
                return result;
        }

        float3 center = em.HasComponent<Unity.Transforms.LocalToWorld>(owner)
            ? em.GetComponentData<Unity.Transforms.LocalToWorld>(owner).Position
            : float3.zero;

        var requestEntity = em.CreateEntity(typeof(VOBYReconstructionRequest));
        em.SetComponentData(requestEntity, new VOBYReconstructionRequest
        {
            reconstructionPoint = reconstructionPoint != null ? (float3)reconstructionPoint.position : center,
            VOBYShape = vobyShape,
            randomizeVOBs = randomizeVOBs,
            freezeUnbatchedVOBs = freezeUnbatchedVOBs,
            batchSize = batchSize,
            batchDelay = batchDelay,
            animationDuration = animationDuration,
            reconstructionType = reconstructionType,
            epicenter = center, // spiral around the shape itself
            useAssignedTargets = true
        });

        currentState = VOBYState.Reconstructed;
        lastMessage = $"Morphing into {formation.name}.";
        if (SoundManager.Instance != null) SoundManager.Instance.PlayReconstructionSound();
        return result;
    }

    /// <summary>Cubes the player owns right now.</summary>
    public int PlayerCubeCount => WorldReady() ? FormationMorphUtility.GetCubeCount(ecsWorld.EntityManager, GetControlledVOBY()) : 0;

    public bool CanMorphInto(VOBFormation formation, out int missing)
    {
        int have = PlayerCubeCount;
        missing = formation != null ? Mathf.Max(0, formation.Cost - have) : 0;
        return formation != null && missing == 0;
    }

    /// <summary>
    /// Knocks cubes off the player at <paramref name="hitPoint"/>; they become loose pickups.
    /// Hook damage into this: DetachCubes(damageInCubes, hit.point, knockback).
    /// </summary>
    public int DetachCubes(int count, Vector3 hitPoint, float force)
    {
        if (!WorldReady()) return 0;
        int n = FormationMorphUtility.DetachVOBs(ecsWorld.EntityManager, GetControlledVOBY(), count, hitPoint, force, detachPickupDelay);
        lastMessage = $"Lost {n} cubes.";
        return n;
    }

    [Button("Drop Test Cubes")]
    public void DetachTestCubes()
    {
        if (!WorldReady()) return;
        var em = ecsWorld.EntityManager;
        Entity owner = GetControlledVOBY();
        if (owner == Entity.Null) return;
        float3 center = em.GetComponentData<Unity.Transforms.LocalToWorld>(owner).Position;
        // Hit from a random side so different parts get chipped off.
        Vector3 side = UnityEngine.Random.onUnitSphere;
        side.y = Mathf.Abs(side.y) * 0.5f;
        DetachCubes(detachTestCount, (Vector3)center + side.normalized * 5f, detachForce);
    }

    [Button("Toggle Collecting")]
    public void ToggleCollecting()
    {
        if (!WorldReady()) return;
        var em = ecsWorld.EntityManager;
        Entity owner = GetControlledVOBY();
        if (owner == Entity.Null || !em.HasComponent<VOBCollector>(owner))
        {
            lastMessage = "The controlled VOBY has no collector. Tick 'Is Player' on its VOBYAuthoring.";
            Debug.LogWarning(lastMessage);
            return;
        }
        var collector = em.GetComponentData<VOBCollector>(owner);
        collector.Enabled = !collector.Enabled;
        em.SetComponentData(owner, collector);
        lastMessage = $"Collecting {(collector.Enabled ? "on" : "off")}.";
    }

    void HandleFormationHotkeys()
    {
        if (enableNumberHotkeys)
            for (int i = 0; i < hotkeyFormations.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && hotkeyFormations[i] != null)
                    MorphTo(hotkeyFormations[i]);
        if (Input.GetKeyDown(KeyCode.X)) DetachTestCubes();
        if (Input.GetKeyDown(KeyCode.C)) ToggleCollecting();
    }

    void LateUpdate()
    {
        if (playerAnchor == null || !WorldReady()) return;
        var em = ecsWorld.EntityManager;
        Entity owner = GetControlledVOBY();
        if (owner == Entity.Null || !em.HasComponent<Unity.Transforms.LocalTransform>(owner)) return;
        var t = em.GetComponentData<Unity.Transforms.LocalTransform>(owner);
        t.Position = playerAnchor.position;
        t.Rotation = playerAnchor.rotation;
        em.SetComponentData(owner, t);
    }

    Entity GetControlledVOBY()
    {
        var em = ecsWorld.EntityManager;
        if (controlledVOBY == Entity.Null || !em.Exists(controlledVOBY))
            controlledVOBY = FormationMorphUtility.FindControlledVOBY(em);
        return controlledVOBY;
    }

    bool WorldReady() => ecsWorld != null && ecsWorld.IsCreated;

    void OnGUI()
    {
        if (!showDebugHUD || !Application.isPlaying || !WorldReady()) return;
        int have = PlayerCubeCount;

        GUILayout.BeginArea(new Rect(12, 12, 340, 400), GUI.skin.box);
        GUILayout.Label($"<b>Cubes: {have}</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });
        for (int i = 0; i < hotkeyFormations.Length && i < 9; i++)
        {
            var f = hotkeyFormations[i];
            if (f == null) continue;
            string need = have >= f.Cost ? "ready" : $"need {f.Cost - have} more";
            GUILayout.Label($"[{i + 1}] {f.name} - {f.Cost} cubes ({need})");
        }
        GUILayout.Label("X: knock off cubes   C: collecting on/off");
        if (!string.IsNullOrEmpty(lastMessage)) GUILayout.Label(lastMessage);
        GUILayout.EndArea();
    }
    #endregion
}
