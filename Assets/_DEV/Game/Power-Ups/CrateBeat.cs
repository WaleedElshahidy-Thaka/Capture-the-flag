using UnityEngine;

// Makes the power-up crate "beat" like a heart: ba-dum ... ba-dum (visual only, no networking).
//
// Scales only `target` (the intact vase mesh, e.g. SM_Vase_D01/Fixed/SM_Vase_D01_Fixed). CrateBreakFX
// scales its parent (Fixed) for the respawn pop-in, so the two stack and never fight.
// Put it on the crate root, next to CrateBreakFX.
public class CrateBeat : MonoBehaviour
{
    [Tooltip("The mesh to pulse (a child of the object CrateBreakFX pops in).")]
    [SerializeField] Transform target;
    [Tooltip("How much bigger at the strongest thump (0.08 = +8%).")]
    [SerializeField, Range(0f, 0.5f)] float amount = 0.08f;
    [Tooltip("Heartbeats per second.")]
    [SerializeField, Min(0.05f)] float beatsPerSecond = 1f;
    [Tooltip("Size of the second thump compared to the first.")]
    [SerializeField, Range(0f, 1f)] float secondThump = 0.6f;

    // Fractions of one beat: where each thump starts and how long it lasts.
    const float FirstStart = 0f, SecondStart = 0.22f, ThumpLength = 0.18f;

    Vector3 baseScale;

    void Awake()
    {
        if (target != null) baseScale = target.localScale;
    }

    void OnDisable()
    {
        if (target != null) target.localScale = baseScale;
    }

    void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;

        float phase = Mathf.Repeat(Time.time * beatsPerSecond, 1f);
        float pulse = Mathf.Max(Thump(phase, FirstStart), secondThump * Thump(phase, SecondStart));
        target.localScale = baseScale * (1f + amount * pulse);
    }

    // 0 -> 1 -> 0 smoothly over ThumpLength, 0 outside it.
    static float Thump(float phase, float start)
    {
        float t = (phase - start) / ThumpLength;
        return t <= 0f || t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI);
    }
}
