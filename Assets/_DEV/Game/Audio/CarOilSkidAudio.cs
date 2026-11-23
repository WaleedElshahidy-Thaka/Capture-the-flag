using UnityEngine;

// Slippery tyre-skid sound while this car drives over an Oil Slick patch.
//
// Uses the same check the drive model uses (OilSurfaceRegistry.IsOilAt), so the sound matches the real
// slippery area. Speed comes from how far the car moved this frame, so it works for every car on every
// peer (no physics state needed). The loop gets louder and higher with speed, and fades out off the oil.
// Audio only - no networking of its own. Put it on the car root (next to PlayerMovement).
public class CarOilSkidAudio : MonoBehaviour
{
    [SerializeField] PlayerMovement movement;
    [SerializeField] AudioSource loopSource;
    [SerializeField] AudioClip skidLoopClip;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float maxVolume = 0.8f;
    [Tooltip("Speed (m/s) at which the skid is at full volume. Slower = quieter.")]
    [SerializeField, Min(0.1f)] float fullVolumeSpeed = 10f;
    [Tooltip("Below this speed (m/s) there's no skid.")]
    [SerializeField, Min(0f)] float minSpeed = 1.5f;
    [SerializeField] Vector2 pitchRange = new Vector2(0.85f, 1.2f);
    [SerializeField, Min(0.01f)] float fadeInSeconds = 0.08f;
    [SerializeField, Min(0.01f)] float fadeOutSeconds = 0.25f;

    Vector3 lastPosition;
    bool hasLast;
    float level;     // 0..1
    float speed;     // smoothed m/s

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (loopSource != null)
        {
            loopSource.clip = skidLoopClip;
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            loopSource.volume = 0f;
        }
    }

    void Update()
    {
        if (loopSource == null || skidLoopClip == null) return;
        if (movement == null || movement.Object == null || !movement.Object.IsValid || movement.Runner == null) return;

        // Speed from the visible movement (works for remote cars too).
        Vector3 pos = transform.position;
        if (hasLast && Time.deltaTime > 0f)
        {
            Vector3 delta = pos - lastPosition;
            delta.y = 0f;
            speed = Mathf.Lerp(speed, delta.magnitude / Time.deltaTime, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }
        lastPosition = pos;
        hasLast = true;

        bool onOil = speed > minSpeed && OilSurfaceRegistry.IsOilAt(movement.Runner, pos);
        float speed01 = Mathf.InverseLerp(minSpeed, fullVolumeSpeed, speed);
        float target = onOil ? Mathf.Lerp(0.35f, 1f, speed01) : 0f;

        float rate = 1f / (target > level ? fadeInSeconds : fadeOutSeconds);
        level = Mathf.MoveTowards(level, target, rate * Time.deltaTime);

        loopSource.volume = level * maxVolume;
        loopSource.pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, speed01);

        if (level > 0f && !loopSource.isPlaying) loopSource.Play();
        else if (level <= 0f && loopSource.isPlaying) loopSource.Stop();
    }
}
