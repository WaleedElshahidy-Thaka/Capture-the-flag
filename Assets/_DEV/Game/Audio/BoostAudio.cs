using UnityEngine;

// Overdrive power-up sound. (Drift boost stages have their own sounds in RobotAudioController.)
//
//   Overdrive starts   -> Activate one-shot ("VMMMMM-SHOOOOOM"), the Loop fades in
//   Overdrive ends     -> End one-shot (descending pitch), the Loop fades out
//
// Follows PlayerMovement.IsOverdriveActive (networked state), so every peer hears every car's Overdrive,
// positioned in 3D. Audio only - no networking of its own. Put it on the car root.
public class BoostAudio : MonoBehaviour
{
    [SerializeField] PlayerMovement movement;

    [Header("Sources")]
    [Tooltip("Plays Activate and End (one-shots).")]
    [SerializeField] AudioSource oneShotSource;
    [Tooltip("Plays the looping engine layer while boosting.")]
    [SerializeField] AudioSource loopSource;

    [Header("Clips")]
    [SerializeField] AudioClip activateClip;
    [SerializeField] AudioClip loopClip;
    [SerializeField] AudioClip endClip;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float activateVolume = 1f;
    [SerializeField, Range(0f, 1f)] float loopVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] float endVolume = 0.8f;
    [SerializeField, Min(0.01f)] float loopFadeInSeconds = 0.3f;
    [SerializeField, Min(0.01f)] float loopFadeOutSeconds = 0.15f;

    bool wasActive;
    float loopLevel;   // 0..1, scaled by loopVolume

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (loopSource != null)
        {
            loopSource.clip = loopClip;
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            loopSource.volume = 0f;
        }
        if (oneShotSource != null) oneShotSource.playOnAwake = false;
    }

    void Update()
    {
        if (movement == null || movement.Object == null || !movement.Object.IsValid) return;

        bool active = movement.IsOverdriveActive;

        if (active && !wasActive) PlayOneShot(activateClip, activateVolume);
        else if (!active && wasActive) PlayOneShot(endClip, endVolume);

        wasActive = active;
        UpdateLoop(active);
    }

    void UpdateLoop(bool active)
    {
        if (loopSource == null || loopClip == null) return;

        float speed = 1f / (active ? loopFadeInSeconds : loopFadeOutSeconds);
        loopLevel = Mathf.MoveTowards(loopLevel, active ? 1f : 0f, speed * Time.deltaTime);
        loopSource.volume = loopLevel * loopVolume;

        if (loopLevel > 0f && !loopSource.isPlaying) loopSource.Play();
        else if (loopLevel <= 0f && loopSource.isPlaying) loopSource.Stop();
    }

    void PlayOneShot(AudioClip clip, float volume)
    {
        if (oneShotSource != null && clip != null) oneShotSource.PlayOneShot(clip, volume);
    }
}
