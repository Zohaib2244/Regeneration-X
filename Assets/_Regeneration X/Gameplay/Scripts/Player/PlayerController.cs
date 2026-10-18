using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down character controller for the cube body.
///
/// This GameObject is the player's real position (a CharacterController, so normal PhysX walls work).
/// The cube VOBY follows <see cref="bodyPivot"/>, a child that carries the visual rotation: facing,
/// lean while moving, or rolling like a ball. ECSCommandBridge copies that pivot onto the VOBY every frame.
///
/// Controls:  WASD / left stick move · Space / A dodge · 1-9 morph into a form ·
///            Q / E (or shoulder buttons) previous / next form · R back to the starting form
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public ECSCommandBridge bridge;
    [Tooltip("The cube body follows this. Created automatically if left empty.")]
    public Transform bodyPivot;
    public Camera aimCamera;

    [Header("Forms")]
    [Tooltip("Stats used at the start. If it has a formation, R morphs back to it.")]
    public PlayerFormDefinition startingForm;
    [Tooltip("Keys 1-9 morph into these, in order.")]
    public PlayerFormDefinition[] forms = new PlayerFormDefinition[0];

    [Header("Movement (when no form is active)")]
    public float baseMoveSpeed = 6f;
    public float baseAcceleration = 40f;
    public float baseTurnSpeed = 720f;

    [Header("World")]
    [Tooltip("Floor height. The player never goes below this, so no PhysX floor is needed (the cubes' own floor lives in the DOTS physics world).")]
    public float groundY = 0f;
    public float gravity = -25f;

    [Header("Dodge")]
    public float dodgeSpeed = 16f;
    public float dodgeDuration = 0.22f;
    public float dodgeCooldown = 0.6f;

    [Header("Body feel")]
    [Tooltip("Face the aim direction (mouse / right stick) instead of the movement direction.")]
    public bool faceAim = false;
    [Tooltip("How far the body leans into its movement, in degrees.")]
    public float leanDegrees = 8f;

    [Header("HUD")]
    public bool showHUD = true;

    // ---- read by the camera / other systems
    public Vector3 PlanarVelocity => planarVelocity;
    public float CurrentRadius => smoothedRadius;
    public Vector3 FocusPoint => bodyPivot != null ? bodyPivot.position + Vector3.up * (formHeight * 0.5f) : transform.position;
    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public PlayerFormDefinition CurrentForm => currentForm;
    public bool IsDodging => dodgeTimer > 0f;

    CharacterController cc;
    PlayerFormDefinition currentForm;

    Vector3 planarVelocity;
    float verticalVelocity;
    bool grounded;

    float yaw;
    float yawWeight = 1f;        // 1 = body turns to face travel, 0 = body only rolls
    Quaternion rollRotation = Quaternion.identity;
    Quaternion leanRotation = Quaternion.identity;

    float dodgeTimer, dodgeCooldownTimer;
    Vector3 dodgeDirection;

    float formHeight = 2f;
    float rollRadius = 0.5f;
    float targetRadius = 1f;
    float smoothedRadius = 1f;
    float pivotOffsetY;

    string message;
    float messageUntil;

    static readonly Key[] DigitKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    float Speed => currentForm != null ? currentForm.moveSpeed : baseMoveSpeed;
    float Acceleration => currentForm != null ? currentForm.acceleration : baseAcceleration;
    float TurnSpeed => currentForm != null ? currentForm.turnSpeed : baseTurnSpeed;
    bool IsRolling => currentForm != null && currentForm.moveStyle == PlayerFormDefinition.MoveStyle.Roll;
    bool CanDodge => currentForm == null || currentForm.canDodge;

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (bridge == null) bridge = FindFirstObjectByType<ECSCommandBridge>();
        if (aimCamera == null) aimCamera = Camera.main;

        if (bodyPivot == null)
        {
            var go = new GameObject("BodyPivot");
            go.transform.SetParent(transform, false);
            bodyPivot = go.transform;
        }

        if (bridge != null)
        {
            bridge.playerAnchor = bodyPivot;   // the VOBY follows the pivot
            bridge.showDebugHUD = false;       // this component draws the HUD
            bridge.enableNumberHotkeys = false;
        }
        else
        {
            Debug.LogWarning("PlayerController: no ECSCommandBridge in the scene, so nothing will move the cubes.", this);
        }

        yaw = transform.eulerAngles.y;
        ApplyForm(startingForm);
        smoothedRadius = targetRadius;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        HandleMorphInput();

        Vector3 wish = ReadMoveDirection(out float inputMagnitude);
        UpdateAim();
        UpdateDodgeAndVelocity(wish, inputMagnitude, dt);
        MoveCharacter(dt);
        UpdateBody(dt);

        smoothedRadius = Mathf.Lerp(smoothedRadius, targetRadius, 1f - Mathf.Exp(-4f * dt));
    }

    // ------------------------------------------------------------------ input

    Vector3 ReadMoveDirection(out float magnitude)
    {
        Vector2 v = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
        }
        var gp = Gamepad.current;
        if (gp != null) v += gp.leftStick.ReadValue();

        v = Vector2.ClampMagnitude(v, 1f);
        magnitude = v.magnitude;
        if (magnitude < 0.01f) return Vector3.zero;

        // Relative to the camera so "up" always means up the screen.
        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (aimCamera != null)
        {
            fwd = Vector3.ProjectOnPlane(aimCamera.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(aimCamera.transform.up, Vector3.up);
            fwd.Normalize();
            right = Vector3.Cross(Vector3.up, fwd);
        }
        return fwd * v.y + right * v.x;
    }

    void UpdateAim()
    {
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 stick = gp.rightStick.ReadValue();
            if (stick.sqrMagnitude > 0.04f && aimCamera != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(aimCamera.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                AimDirection = (fwd * stick.y + right * stick.x).normalized;
                return;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && aimCamera != null)
        {
            Ray ray = aimCamera.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, transform.position + Vector3.up);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 flat = ray.GetPoint(enter) - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f) AimDirection = flat.normalized;
            }
        }
    }

    void HandleMorphInput()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        if (kb != null)
        {
            for (int i = 0; i < DigitKeys.Length && i < forms.Length; i++)
                if (kb[DigitKeys[i]].wasPressedThisFrame) TryMorph(forms[i]);

            if (kb.qKey.wasPressedThisFrame) CycleForm(-1);
            if (kb.eKey.wasPressedThisFrame) CycleForm(+1);
            if (kb.rKey.wasPressedThisFrame) TryMorph(startingForm);
        }
        if (gp != null)
        {
            if (gp.leftShoulder.wasPressedThisFrame) CycleForm(-1);
            if (gp.rightShoulder.wasPressedThisFrame) CycleForm(+1);
            if (gp.buttonNorth.wasPressedThisFrame) TryMorph(startingForm);
        }
    }

    void CycleForm(int step)
    {
        if (forms.Length == 0) return;
        int index = System.Array.IndexOf(forms, currentForm);
        // From a form that isn't in the list, "next" goes to the first and "previous" to the last.
        index = index < 0 ? (step > 0 ? 0 : forms.Length - 1) : (index + step + forms.Length) % forms.Length;
        TryMorph(forms[index]);
    }

    // ------------------------------------------------------------------ morphing

    /// <summary>Morphs into <paramref name="form"/> if the player owns enough cubes.</summary>
    public bool TryMorph(PlayerFormDefinition form)
    {
        if (form == null || form.formation == null || bridge == null) return false;
        if (form == currentForm) { Say($"Already {form.name}."); return false; }

        if (!bridge.CanMorphInto(form.formation, out int missing))
        {
            Say($"Need {missing} more cubes for {form.name} (costs {form.Cost}).");
            return false;
        }

        // Per-form morph feel.
        bridge.batchSize = form.morphBatchSize;
        bridge.batchDelay = form.morphBatchDelay;
        bridge.animationDuration = form.morphAnimationDuration;
        bridge.reconstructionType = form.transition;

        MorphResult result = bridge.MorphTo(form.formation);
        switch (result)
        {
            case MorphResult.Started:
                ApplyForm(form);
                Say($"{form.name}!");
                return true;
            case MorphResult.Busy:
                Say("Still morphing...");
                return false;
            default:
                Say("Can't morph right now.");
                return false;
        }
    }

    /// <summary>Switches stats, collider, camera size and where the body sits for a form.</summary>
    void ApplyForm(PlayerFormDefinition form)
    {
        currentForm = form;
        if (form == null || form.formation == null || form.formation.Cost == 0) return;

        VOBFormation f = form.formation;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (Vector3 s in f.slots)
        {
            min = Vector3.Min(min, s);
            max = Vector3.Max(max, s);
        }

        float half = f.cubeSize * 0.5f;
        float height = (max.y - min.y) + f.cubeSize;
        float width = Mathf.Max(max.x - min.x, max.z - min.z) + f.cubeSize;

        // Capsule that wraps the shape. A capsule's radius can't exceed half its height.
        float radius = Mathf.Clamp(width * 0.5f, 0.2f, Mathf.Max(0.2f, height * 0.5f));
        cc.radius = radius;
        cc.height = Mathf.Max(height, radius * 2f);
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

        formHeight = cc.height;
        rollRadius = Mathf.Max(0.1f, Mathf.Max(width, height) * 0.5f);
        targetRadius = f.BoundsRadius;

        // Lift the body so the bottom of its lowest cube rests on the ground, whatever the formation's pivot.
        pivotOffsetY = half - min.y;
    }

    // ------------------------------------------------------------------ movement

    void UpdateDodgeAndVelocity(Vector3 wish, float inputMagnitude, float dt)
    {
        dodgeCooldownTimer -= dt;

        if (dodgeTimer > 0f)
        {
            dodgeTimer -= dt;
            planarVelocity = dodgeDirection * dodgeSpeed;
            return;
        }

        Vector3 target = wish * (Speed * Mathf.Clamp01(inputMagnitude));
        planarVelocity = Vector3.MoveTowards(planarVelocity, target, Acceleration * dt);

        bool dodgePressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (dodgePressed && CanDodge && dodgeCooldownTimer <= 0f)
        {
            // Dodge where you're heading; if standing still, where you're facing.
            dodgeDirection = wish.sqrMagnitude > 0.01f ? wish.normalized
                : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            dodgeTimer = dodgeDuration;
            dodgeCooldownTimer = dodgeDuration + dodgeCooldown;
        }
    }

    void MoveCharacter(float dt)
    {
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dt;

        // Never fall through the floor, even without any PhysX collider under us.
        float fall = verticalVelocity * dt;
        float maxFall = groundY - transform.position.y;
        bool clamped = fall < maxFall;
        if (clamped) { fall = maxFall; verticalVelocity = 0f; }

        cc.Move(planarVelocity * dt + Vector3.up * fall);
        grounded = clamped || cc.isGrounded;
    }

    // ------------------------------------------------------------------ the cube body's pose

    void UpdateBody(float dt)
    {
        float speed = planarVelocity.magnitude;
        Vector3 travel = speed > 0.05f ? planarVelocity / speed : Vector3.zero;

        // Facing.
        if (faceAim && AimDirection.sqrMagnitude > 0.01f && !IsDodging)
            yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(AimDirection.x, AimDirection.z) * Mathf.Rad2Deg, TurnSpeed * dt);
        else if (speed > 0.3f)
            yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg, TurnSpeed * dt);

        // Rolling: spin around the axis perpendicular to travel, by the distance covered.
        if (IsRolling && speed > 0.05f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, travel);
            float degrees = speed * dt / rollRadius * Mathf.Rad2Deg;
            rollRotation = Quaternion.AngleAxis(degrees, axis) * rollRotation;
        }
        else if (!IsRolling)
        {
            // Leaving a roll: settle the body back upright (cubes track the pivot, so this looks like a smooth unroll).
            rollRotation = Quaternion.Slerp(rollRotation, Quaternion.identity, 1f - Mathf.Exp(-10f * dt));
        }

        // Turning to face travel only applies when walking; blend so switching form doesn't pop.
        yawWeight = Mathf.MoveTowards(yawWeight, IsRolling ? 0f : 1f, dt / 0.3f);
        Quaternion facing = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(0f, yaw, 0f), yawWeight);

        // Lean into the movement.
        Quaternion targetLean = Quaternion.identity;
        if (!IsRolling && travel != Vector3.zero)
        {
            float amount = leanDegrees * Mathf.Clamp01(speed / Mathf.Max(0.1f, Speed));
            targetLean = Quaternion.AngleAxis(amount, Vector3.Cross(Vector3.up, travel));
        }
        leanRotation = Quaternion.Slerp(leanRotation, targetLean, 1f - Mathf.Exp(-8f * dt));

        bodyPivot.rotation = leanRotation * facing * rollRotation;

        Vector3 local = bodyPivot.localPosition;
        local.y = Mathf.Lerp(local.y, pivotOffsetY, 1f - Mathf.Exp(-6f * dt));
        bodyPivot.localPosition = local;
    }

    // ------------------------------------------------------------------ HUD

    void Say(string text)
    {
        message = text;
        messageUntil = Time.time + 2.5f;
    }

    void OnGUI()
    {
        if (!showHUD || !Application.isPlaying) return;

        int cubes = bridge != null ? bridge.PlayerCubeCount : 0;

        GUILayout.BeginArea(new Rect(12, 12, 380, 460), GUI.skin.box);
        GUILayout.Label($"<b>Cubes: {cubes}</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 18 });

        for (int i = 0; i < forms.Length && i < 9; i++)
        {
            var f = forms[i];
            if (f == null) continue;
            bool active = f == currentForm;
            bool ready = cubes >= f.Cost;
            string state = active ? "ACTIVE" : ready ? "ready" : $"need {f.Cost - cubes} more";
            string hex = ColorUtility.ToHtmlStringRGB(f.uiColor);
            GUILayout.Label($"[{i + 1}] <color=#{hex}>{f.name}</color> - {f.Cost} cubes ({state})",
                new GUIStyle(GUI.skin.label) { richText = true, fontStyle = active ? FontStyle.Bold : FontStyle.Normal });
        }

        GUILayout.Space(4);
        GUILayout.Label("WASD move  Space dodge  1-9 / Q E morph  R base form\nX knock off cubes  C collecting on/off");
        if (!string.IsNullOrEmpty(message) && Time.time < messageUntil) GUILayout.Label(message);
        GUILayout.EndArea();
    }
}
