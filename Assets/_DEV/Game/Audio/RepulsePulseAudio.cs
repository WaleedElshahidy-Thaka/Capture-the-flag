using UnityEngine;

// Repulse Pulse: the per-car hit impacts and the recovery tail. (Charging and the BOOM use RepulsePulse's
// own Charge Audio / Pulse Audio slots.)
//
//   Another car's pulse fires and this car is inside it -> Hit, delayed by distance so it lands as the
//                                                          shockwave reaches this car
//   This car's pulse fires                               -> Recovery tail, shortly after the BOOM
//
// Listens to RepulsePulse.Fired, which every peer raises when the replicated PulseCount changes, and uses
// the same reach as the push (radius on the ground, max height difference). Audio only - no networking of
// its own, no gameplay change. Put it on the car root, next to RepulsePulse.
public class RepulsePulseAudio : MonoBehaviour
{
    [SerializeField] RepulsePulse pulse;
    [SerializeField] AudioSource source;

    [Header("Clips")]
    [SerializeField] AudioClip hitClip;
    [SerializeField] AudioClip recoveryClip;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float hitVolume = 1f;
    [SerializeField, Range(0f, 1f)] float recoveryVolume = 0.6f;
    [SerializeField, Range(0f, 0.3f)] float hitPitchVariation = 0.12f;

    [Header("Timing")]
    [Tooltip("Seconds the shockwave takes to reach the edge of the radius (matches RepulseShockwaveFX).")]
    [SerializeField, Min(0f)] float waveTravelSeconds = 0.43f;
    [Tooltip("Delay after this car's BOOM before the recovery tail.")]
    [SerializeField, Min(0f)] float recoveryDelay = 0.6f;

    float hitAt = -1f;
    float recoveryAt = -1f;

    void Awake()
    {
        if (pulse == null) pulse = GetComponent<RepulsePulse>();
        if (source != null) source.playOnAwake = false;
    }

    void OnEnable() => RepulsePulse.Fired += OnFired;
    void OnDisable() => RepulsePulse.Fired -= OnFired;

    void OnFired(RepulsePulse firedBy)
    {
        if (firedBy == null) return;

        if (firedBy == pulse)
        {
            recoveryAt = Time.time + recoveryDelay;
            return;
        }

        // Same reach as the push: within the radius on the ground, and not too far above/below.
        Vector3 offset = transform.position - firedBy.transform.position;
        if (Mathf.Abs(offset.y) > HandlingValuesProvider.Current.repulseMaxHeightDifference) return;
        offset.y = 0f;

        float radius = firedBy.Radius;
        float distance = offset.magnitude;
        if (distance > radius) return;

        hitAt = Time.time + waveTravelSeconds * Mathf.Clamp01(distance / Mathf.Max(radius, 0.01f));
    }

    void Update()
    {
        if (hitAt >= 0f && Time.time >= hitAt)
        {
            hitAt = -1f;
            Play(hitClip, hitVolume, hitPitchVariation);
        }
        if (recoveryAt >= 0f && Time.time >= recoveryAt)
        {
            recoveryAt = -1f;
            Play(recoveryClip, recoveryVolume, 0f);
        }
    }

    void Play(AudioClip clip, float volume, float pitchVariation)
    {
        if (source == null || clip == null) return;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, volume);
    }
}
