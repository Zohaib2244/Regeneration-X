using UnityEngine;

/// <summary>
/// A full-body shape the player can take: which formation to build, and how it moves.
/// Make one per shape with NuttyTools > Create Forms From Selected Formations, then tweak the numbers.
/// </summary>
[CreateAssetMenu(menuName = "Regeneration X/Player Form", fileName = "NewForm")]
public class PlayerFormDefinition : ScriptableObject
{
    public enum MoveStyle
    {
        [Tooltip("Upright body that turns to face where it's going.")]
        Walk,
        [Tooltip("The whole body rolls in the direction of travel, like a ball. Momentum-heavy.")]
        Roll
    }

    [Header("Shape")]
    public VOBFormation formation;
    [Tooltip("Colour used for this form in the HUD.")]
    public Color uiColor = Color.white;
    [TextArea] public string description;

    [Header("Movement")]
    public MoveStyle moveStyle = MoveStyle.Walk;
    public float moveSpeed = 6f;
    [Tooltip("How quickly it reaches full speed (m/s^2). Low = slidey, high = snappy.")]
    public float acceleration = 40f;
    [Tooltip("Degrees per second the body can turn (Walk style).")]
    public float turnSpeed = 720f;
    public bool canDodge = true;

    [Header("Morphing into this form")]
    public ReconstructionType transition = ReconstructionType.Default;
    [Tooltip("Cubes released per step. Higher = faster, more explosive morph.")]
    public int morphBatchSize = 100;
    public float morphBatchDelay = 0.02f;
    [Tooltip("Seconds each cube takes to fly into place.")]
    public float morphAnimationDuration = 0.3f;

    public int Cost => formation != null ? formation.Cost : 0;
}
