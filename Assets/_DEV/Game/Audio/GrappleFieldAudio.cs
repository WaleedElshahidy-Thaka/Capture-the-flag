using UnityEngine;

// Grapple Field sound: magnetic attraction + electronic targeting.
//
//   Turns on                     -> Activate (quick targeting pulses), the magnetic hum Loop fades in
//   Takes the Crown while on     -> Steal (magnetic ZAP + confirmation tone) instead of the shutdown
//   Ends without a steal         -> End (electronic shutdown), the Loop fades out
//
// Follows GrappleField.IsActive and the Crown holder (both networked), so every peer hears every car's
// Grapple in 3D. Audio only - no networking of its own. Put it on the car root, next to GrappleField.
public class GrappleFieldAudio : MonoBehaviour
{
    [SerializeField] GrappleField grapple;

    [Header("Sources")]
    [SerializeField] AudioSource oneShotSource;
    [SerializeField] AudioSource loopSource;

    [Header("Clips")]
    [SerializeField] AudioClip activateClip;
    [SerializeField] AudioClip loopClip;
    [SerializeField] AudioClip stealClip;
    [SerializeField] AudioClip endClip;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float activateVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] float loopVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] float stealVolume = 1f;
    [SerializeField, Range(0f, 1f)] float endVolume = 0.8f;
    [SerializeField, Min(0.01f)] float loopFadeInSeconds = 0.4f;
    [SerializeField, Min(0.01f)] float loopFadeOutSeconds = 0.15f;

    bool initialized;
    bool wasActive;
    bool wasHolder;
    bool stoleThisRun;
    float loopLevel;

    void Awake()
    {
        if (grapple == null) grapple = GetComponent<GrappleField>();
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
        if (grapple == null || grapple.Object == null || !grapple.Object.IsValid) return;

        bool active = grapple.IsActive;
        var crown = CrownMatchState.Instance;
        bool holder = crown != null && crown.IsHolder(grapple.Object);

        // First valid frame (spawn / join mid-match): take the current state, play nothing.
        if (!initialized)
        {
            initialized = true;
            wasActive = active;
            wasHolder = holder;
            return;
        }

        if (active && !wasActive)
        {
            stoleThisRun = false;
            PlayOneShot(activateClip, activateVolume);
        }

        // The steal ends Grapple on the same tick, so accept the Crown arriving just as it switches off.
        if (holder && !wasHolder && (active || wasActive) && !stoleThisRun)
        {
            stoleThisRun = true;
            PlayOneShot(stealClip, stealVolume);
        }

        if (!active && wasActive && !stoleThisRun)
            PlayOneShot(endClip, endVolume);

        wasActive = active;
        wasHolder = holder;

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
