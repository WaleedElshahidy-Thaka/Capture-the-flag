using System.Collections.Generic;
using UnityEngine;

// Damper Field sound: deep resonating energy shield.
//
//   Turns on                       -> Activate (low THUM + shield expansion), the harmonic Loop fades in
//   Another car touches the bubble -> Hit (metallic-energy BOING), with a little pitch variation
//   Ends                           -> End (collapse with a descending shimmer), the Loop fades out
//
// Follows DamperField.IsActive (networked). A "hit" is worked out on each peer from where the cars are:
// another car's body entering the shield bubble. Audio only - no networking of its own, no gameplay
// change. Put it on the car root, next to DamperField.
public class DamperFieldAudio : MonoBehaviour
{
    [SerializeField] DamperField damper;

    [Header("Sources")]
    [SerializeField] AudioSource oneShotSource;
    [SerializeField] AudioSource loopSource;

    [Header("Clips")]
    [SerializeField] AudioClip activateClip;
    [SerializeField] AudioClip loopClip;
    [SerializeField] AudioClip hitClip;
    [SerializeField] AudioClip endClip;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float activateVolume = 1f;
    [SerializeField, Range(0f, 1f)] float loopVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] float hitVolume = 1f;
    [SerializeField, Range(0f, 1f)] float endVolume = 0.85f;
    [SerializeField, Min(0.01f)] float loopFadeInSeconds = 0.5f;
    [SerializeField, Min(0.01f)] float loopFadeOutSeconds = 0.2f;

    [Header("Hit")]
    [Tooltip("Centre of the shield bubble (e.g. View/Sphere).")]
    [SerializeField] Transform shieldCentre;
    [Tooltip("Bubble radius in metres (the Damper bubble is 2.2 m wide).")]
    [SerializeField, Min(0.1f)] float shieldRadius = 1.1f;
    [Tooltip("Minimum time between two hit sounds, so a long scrape doesn't spam.")]
    [SerializeField, Min(0f)] float hitCooldown = 0.35f;
    [SerializeField, Range(0f, 0.3f)] float hitPitchVariation = 0.1f;

    readonly HashSet<PlayerMatchState> inside = new HashSet<PlayerMatchState>();
    readonly List<PlayerMatchState> leftThisFrame = new List<PlayerMatchState>();
    readonly Dictionary<PlayerMatchState, BoxCollider> bodies = new Dictionary<PlayerMatchState, BoxCollider>();

    bool initialized;
    bool wasActive;
    float loopLevel;
    float nextHitTime;

    void Awake()
    {
        if (damper == null) damper = GetComponent<DamperField>();
        if (shieldCentre == null) shieldCentre = transform;
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
        if (damper == null || damper.Object == null || !damper.Object.IsValid) return;

        bool active = damper.IsActive;

        // First valid frame (spawn / join mid-match): take the current state, play nothing.
        if (!initialized)
        {
            initialized = true;
            wasActive = active;
            return;
        }

        if (active && !wasActive) PlayOneShot(activateClip, activateVolume, 0f);
        else if (!active && wasActive) PlayOneShot(endClip, endVolume, 0f);
        wasActive = active;

        if (active) CheckHits();
        else inside.Clear();

        UpdateLoop(active);
    }

    // BOING when another car's body enters the bubble (once per entry, rate-limited).
    void CheckHits()
    {
        Vector3 centre = shieldCentre.position;
        float r2 = shieldRadius * shieldRadius;
        var cars = PlayerRoster.Active;

        for (int i = 0; i < cars.Count; i++)
        {
            PlayerMatchState car = cars[i];
            if (car == null || !car.HasState || car.Object == damper.Object) continue;

            if (!bodies.TryGetValue(car, out BoxCollider body) || body == null)
            {
                body = car.GetComponent<BoxCollider>();
                bodies[car] = body;
            }
            if (body == null || !body.enabled) continue;

            bool isInside = (body.ClosestPoint(centre) - centre).sqrMagnitude <= r2;
            if (isInside && inside.Add(car) && Time.time >= nextHitTime)
            {
                nextHitTime = Time.time + hitCooldown;
                PlayOneShot(hitClip, hitVolume, hitPitchVariation);
            }
            else if (!isInside) inside.Remove(car);
        }

        // Forget cars that left the match.
        leftThisFrame.Clear();
        foreach (var car in inside) if (car == null || !car.HasState) leftThisFrame.Add(car);
        foreach (var car in leftThisFrame) inside.Remove(car);
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

    void PlayOneShot(AudioClip clip, float volume, float pitchVariation)
    {
        if (oneShotSource == null || clip == null) return;
        oneShotSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        oneShotSource.PlayOneShot(clip, volume);
    }
}
