using UnityEngine;

/// <summary>
/// Angled top-down follow camera. It pulls back as the player's current shape gets bigger,
/// so a 1000-cube sphere fits on screen as comfortably as a 150-cube humanoid.
/// </summary>
public class TopDownCamera : MonoBehaviour
{
    public PlayerController target;

    [Header("Angle")]
    [Range(20f, 85f)] public float pitch = 55f;
    [Tooltip("Rotate the whole view around the player. 45 gives the diagonal dungeon-crawler look.")]
    public float yaw = 0f;

    [Header("Distance")]
    public float baseDistance = 12f;
    [Tooltip("Extra distance per unit of the shape's radius.")]
    public float radiusZoom = 2.5f;

    [Header("Follow")]
    public float followSharpness = 8f;
    [Tooltip("Look this far ahead in the direction of movement.")]
    public float lookAhead = 1.5f;

    Vector3 lookAheadOffset;

    void LateUpdate()
    {
        if (target == null)
        {
            target = FindFirstObjectByType<PlayerController>();
            if (target == null) return;
        }

        float dt = Time.deltaTime;

        Vector3 velocity = target.PlanarVelocity;
        Vector3 wantedAhead = velocity.sqrMagnitude > 0.25f ? velocity.normalized * lookAhead : Vector3.zero;
        lookAheadOffset = Vector3.Lerp(lookAheadOffset, wantedAhead, 1f - Mathf.Exp(-3f * dt));

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        float distance = baseDistance + target.CurrentRadius * radiusZoom;
        Vector3 wantedPosition = target.FocusPoint + lookAheadOffset - rotation * Vector3.forward * distance;

        transform.position = Vector3.Lerp(transform.position, wantedPosition, 1f - Mathf.Exp(-followSharpness * dt));
        transform.rotation = rotation;
    }

    /// <summary>Jump straight to the target, e.g. after a teleport.</summary>
    public void Snap()
    {
        if (target == null) return;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        float distance = baseDistance + target.CurrentRadius * radiusZoom;
        transform.position = target.FocusPoint - rotation * Vector3.forward * distance;
        transform.rotation = rotation;
    }
}
