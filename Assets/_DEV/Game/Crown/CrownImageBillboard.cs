using UnityEngine;

// The Crown above the holder as an image instead of the cube. Put it on a child of the car that has a
// SpriteRenderer with the crown art, and assign that child as CrownHolderVisual's Crown Indicator.
// CrownHolderVisual keeps doing what it does now - switching the indicator on for the holder and off
// for everyone else - so nothing about who gets the Crown changes. This only handles how it looks:
//
//   - always faces the camera, kept upright (same as the name tag)
//   - pops in with a little overshoot when the car takes the Crown
//   - bobs and pulses gently while held, so it reads as alive at a distance (FD-02: constant motion)
//
// Purely local and visual - no networking.
[RequireComponent(typeof(SpriteRenderer))]
public class CrownImageBillboard : MonoBehaviour
{
    [Header("Pop in")]
    [SerializeField, Min(0.01f)] float popSeconds = 0.3f;

    [Header("While held")]
    [Tooltip("Up/down float, in metres.")]
    [SerializeField, Min(0f)] float bobHeight = 0.12f;
    [SerializeField, Min(0f)] float bobSpeed = 2.5f;
    [Tooltip("Size breathing, as a fraction of the size (0.06 = 6%).")]
    [SerializeField, Min(0f)] float pulseAmount = 0.06f;
    [SerializeField, Min(0f)] float pulseSpeed = 3f;

    Vector3 restLocalPosition;
    Vector3 restScale;
    float shownAt;
    bool captured;

    void Awake() => Capture();

    void OnEnable()
    {
        Capture();
        shownAt = Time.time;          // CrownHolderVisual just switched us on: play the pop
        transform.localScale = Vector3.zero;
    }

    void OnDisable()
    {
        if (!captured) return;
        transform.localPosition = restLocalPosition;
        transform.localScale = restScale;
    }

    void LateUpdate()
    {
        float t = Time.time - shownAt;

        // Pop: 0 -> slightly past full size -> settle.
        float pop = t < popSeconds ? EaseOutBack(t / popSeconds) : 1f;
        float pulse = 1f + pulseAmount * Mathf.Sin(t * pulseSpeed * Mathf.PI);
        transform.localScale = restScale * (pop * pulse);

        transform.localPosition = restLocalPosition + Vector3.up * (bobHeight * Mathf.Sin(t * bobSpeed));

        CameraFacing.FaceUpright(transform);
    }

    void Capture()
    {
        if (captured) return;
        restLocalPosition = transform.localPosition;
        restScale = transform.localScale;
        captured = true;
    }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }
}
