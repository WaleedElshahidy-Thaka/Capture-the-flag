using UnityEngine;

// The Damper Field's protective bubble (ShieldFX / M_Shield). When the car's Damper Field turns on, the
// bubble RISES out of the ground around the robot and fades in; when it ends, it SINKS back into the
// ground and fades out. Everyone sees it - it follows DamperField.IsActive, which is networked
// (FD-04: "a defensive effect a chaser cannot see reads as cheating").
//
// How the rise works: the bubble sits under a pivot at ground level. Growing the pivot's height from 0
// lifts the bubble out of the floor; shrinking it pushes it back down. The shader's Intensity fades
// with it. If you add a "_Reveal" float (0 = hidden, 1 = shown) to ShieldFX later, it's driven too.
//
// Put it on the car root (next to DamperField). Visual only - no networking of its own.
public class DamperShieldFX : MonoBehaviour
{
    [SerializeField] DamperField damper;

    [Header("Bubble")]
    [Tooltip("An empty object at the robot's FEET (ground level), child of the car's View. The bubble sits inside it.")]
    [SerializeField] Transform groundPivot;
    [Tooltip("The bubble's renderer (a sphere with M_Shield, NO collider), child of the pivot, raised so its " +
             "bottom touches the pivot.")]
    [SerializeField] Renderer bubble;

    [Header("Timing")]
    [SerializeField, Min(0.05f)] float riseSeconds = 0.45f;
    [SerializeField, Min(0.05f)] float sinkSeconds = 0.35f;
    [Tooltip("Width at the start of the rise (1 = full width). A little narrower reads as 'growing up'.")]
    [SerializeField, Range(0.1f, 1f)] float startWidth = 0.7f;

    [Header("Shader")]
    [Tooltip("The shield's overall brightness property in ShieldFX.")]
    [SerializeField] string intensityProperty = "_Intensity";
    [SerializeField] float fullIntensity = 1f;
    [Tooltip("Optional reveal property (0..1). Only used if the shader has it.")]
    [SerializeField] string revealProperty = "_Reveal";

    [Header("Last seconds")]
    [Tooltip("Flicker when the shield is about to drop, so chasers know (needs Damper's duration).")]
    [SerializeField] bool flickerBeforeEnd = true;
    [SerializeField, Min(0f)] float flickerSeconds = 1f;

    float shown;            // 0 = in the ground, 1 = fully up
    bool wasActive;
    float activeSince;
    Vector3 pivotScale = Vector3.one;
    MaterialPropertyBlock block;
    int intensityId, revealId;
    bool hasReveal;

    void Awake()
    {
        if (damper == null) damper = GetComponent<DamperField>();
        if (groundPivot != null) pivotScale = groundPivot.localScale;

        intensityId = Shader.PropertyToID(intensityProperty);
        revealId = Shader.PropertyToID(revealProperty);
        hasReveal = bubble != null && bubble.sharedMaterial != null && bubble.sharedMaterial.HasProperty(revealId);

        Apply(0f, 1f);
    }

    void LateUpdate()
    {
        bool active = damper != null && damper.Object != null && damper.Object.IsValid && damper.IsActive;
        if (active && !wasActive) activeSince = Time.time;
        wasActive = active;

        // Rise / sink.
        float speed = 1f / (active ? riseSeconds : sinkSeconds);
        shown = Mathf.MoveTowards(shown, active ? 1f : 0f, speed * Time.deltaTime);

        // Flicker in the last second of the Damper so chasers can time their hit.
        float flicker = 1f;
        if (active && flickerBeforeEnd)
        {
            float duration = HandlingValuesProvider.Current.damperDurationSeconds;
            float left = duration - (Time.time - activeSince);
            if (left < flickerSeconds) flicker = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 25f));
        }

        Apply(shown, flicker);
    }

    void Apply(float t, float flicker)
    {
        bool visible = t > 0.001f;
        if (bubble != null && bubble.enabled != visible) bubble.enabled = visible;
        if (!visible) return;

        // Rising: ease out with a small overshoot. Sinking uses the same curve backwards.
        float rise = EaseOutBack(t);

        if (groundPivot != null)
        {
            float width = Mathf.Lerp(startWidth, 1f, t);
            groundPivot.localScale = new Vector3(pivotScale.x * width, pivotScale.y * Mathf.Max(0.001f, rise), pivotScale.z * width);
        }

        if (bubble != null)
        {
            block ??= new MaterialPropertyBlock();
            bubble.GetPropertyBlock(block);
            block.SetFloat(intensityId, fullIntensity * Mathf.Clamp01(t) * flicker);
            if (hasReveal) block.SetFloat(revealId, Mathf.Clamp01(t));
            bubble.SetPropertyBlock(block);
        }
    }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }
}
