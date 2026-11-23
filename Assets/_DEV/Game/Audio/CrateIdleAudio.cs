using UnityEngine;

// Power-up crate presence: a quiet ticking hum while it can be collected, and a small click the moment it
// arms (crate_arm_delay after it appears). The break itself is CrateBreakFX's sound.
//
// Follows PowerUpCrate.IsShown / IsAvailable (networked), acting only on changes, so each client hears
// each arm once. Audio only - no networking of its own. Put it on the crate root.
public class CrateIdleAudio : MonoBehaviour
{
    [SerializeField] PowerUpCrate crate;
    [SerializeField] AudioSource loopSource;
    [SerializeField] AudioSource oneShotSource;
    [SerializeField] AudioClip idleLoop;
    [SerializeField] AudioClip armClip;

    [SerializeField, Range(0f, 1f)] float idleVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] float armVolume = 0.7f;
    [SerializeField, Min(0.01f)] float loopFadeSeconds = 0.1f;
    [SerializeField, Range(0f, 0.2f)] float pitchVariation = 0.05f;

    bool initialized, wasAvailable;
    float level;

    void Awake()
    {
        if (crate == null) crate = GetComponent<PowerUpCrate>();
        if (loopSource != null)
        {
            loopSource.clip = idleLoop;
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            loopSource.volume = 0f;
            loopSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation); // crates don't tick in unison
        }
        if (oneShotSource != null) oneShotSource.playOnAwake = false;
    }

    void Update()
    {
        bool valid = crate != null && crate.Object != null && crate.Object.IsValid;
        bool available = valid && crate.IsShown && crate.IsAvailable;

        if (valid && !initialized)
        {
            initialized = true;          // adopt the state on spawn / late join: no click replay
            wasAvailable = available;
        }
        else if (valid && available && !wasAvailable && oneShotSource != null && armClip != null)
        {
            oneShotSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            oneShotSource.PlayOneShot(armClip, armVolume);
        }
        wasAvailable = available;

        if (loopSource == null || loopSource.clip == null) return;
        level = Mathf.MoveTowards(level, available ? idleVolume : 0f, Time.deltaTime / loopFadeSeconds);
        loopSource.volume = level;
        if (level > 0.0001f) { if (!loopSource.isPlaying) loopSource.Play(); }
        else if (loopSource.isPlaying) loopSource.Stop();
    }
}
