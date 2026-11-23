using Fusion;
using UnityEngine;

// The Crown's presence in the arena: telegraph pulses -> spawn switch-on -> hum (free / held) whose
// pitch rises with the holder's possession streak. The Crown is the only synthetic thing in the world;
// its hum is audible arena-wide and never fully silent.
//
//   DataCrown.IsTelegraphing                -> one pulse per second at the spawn point
//   IsTelegraphing -> IsPickupAvailable     -> switch-on, then the FREE hum fades in at the Crown
//   CrownMatchState.HolderId set            -> the HELD hum (tighter, agitated) follows the holder's car
//   holder's PlayerMatchState.CurrentHoldTicks -> 0..+5 semitones over 30 s, smooth, same on every client
//
// MULTIPLAYER: everything above is replicated state, read once per rendered frame and acted on only when
// it changes, so each client hears each event once and the streak pitch matches the real score streak.
// Read-only: no networked variables, no RPCs, no gameplay writes. (Pickup / steal one-shots are
// CrownAudio's job on the cars.) Put it on a scene object (e.g. "ArenaAudio").
public class CrownWorldAudioController : MonoBehaviour
{
    [Header("Emitter (moved to the Crown / its holder)")]
    [SerializeField] Transform emitter;
    [SerializeField] AudioSource humFreeSource;
    [SerializeField] AudioSource humHeldSource;
    [SerializeField] AudioSource oneShotSource;

    [Header("Clips")]
    [SerializeField] AudioClip telegraphPulseClip;
    [SerializeField] AudioClip spawnClip;
    [SerializeField] AudioClip humFreeLoop;
    [SerializeField] AudioClip humHeldLoop;

    [Header("Levels")]
    [SerializeField, Range(0f, 1f)] float humFreeVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] float humHeldVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] float telegraphVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float spawnVolume = 0.9f;
    [SerializeField, Min(0.01f)] float loopFadeSeconds = 0.1f;

    [Header("Telegraph")]
    [SerializeField, Min(0.1f)] float pulseInterval = 1f;

    [Header("Streak pitch")]
    [SerializeField] float maxStreakSemitones = 5f;
    [SerializeField, Min(0.1f)] float streakSecondsToMax = 30f;
    [Tooltip("How fast the pitch follows the streak (semitones per second) - keeps it continuous.")]
    [SerializeField, Min(0.1f)] float pitchSlew = 12f;

    [Header("Distance (Crown: full at 5 m, quietest at arena width, never silent)")]
    [SerializeField, Min(0f)] float fullVolumeDistance = 5f;
    [SerializeField, Min(1f)] float arenaWidth = 150f;
    [Tooltip("Volume left at (and beyond) the arena width - the Crown never goes fully silent.")]
    [SerializeField, Range(0f, 1f)] float farFloor = 0.2f;
    [Tooltip("Height above the Crown / holder's car.")]
    [SerializeField] float emitterHeight = 1.5f;

    DataCrown crown;
    float nextCrownSearch;
    NetworkId cachedHolderId;
    Transform cachedHolderView;
    PlayerMatchState cachedHolderState;

    bool initialized, wasTelegraphing, wasAvailable;
    float nextPulseTime, freeLevel, heldLevel, semitones;

    void Awake()
    {
        if (emitter == null) emitter = transform;
        var rolloff = BuildRolloff();
        SetupLoop(humFreeSource, humFreeLoop, rolloff);
        SetupLoop(humHeldSource, humHeldLoop, rolloff);
        Setup3D(oneShotSource, rolloff);
        if (oneShotSource != null) oneShotSource.loop = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        var match = CrownMatchState.Instance;
        bool live = match != null && match.Object != null && match.Object.IsValid
                    && (MatchTimerState.Instance == null || !MatchTimerState.Instance.Finished);

        if (live && (crown == null || crown.Object == null || !crown.Object.IsValid) && Time.unscaledTime >= nextCrownSearch)
        {
            crown = FindFirstObjectByType<DataCrown>();   // once per spawn, not every frame
            nextCrownSearch = Time.unscaledTime + 0.5f;
        }
        bool crownValid = crown != null && crown.Object != null && crown.Object.IsValid;

        if (!live || !crownValid)
        {
            FadeLoop(humFreeSource, ref freeLevel, 0f, dt);
            FadeLoop(humHeldSource, ref heldLevel, 0f, dt);
            initialized = false;
            return;
        }

        bool telegraphing = crown.IsTelegraphing;
        bool available = crown.IsPickupAvailable;
        bool held = match.HasHolder;

        // First valid frame (spawn / join mid-match): adopt the state, play no one-shot.
        if (!initialized)
        {
            initialized = true;
            wasTelegraphing = telegraphing;
            wasAvailable = available;
            nextPulseTime = telegraphing ? Time.time : float.MaxValue;
        }

        // Telegraph: a pulse now, then one per interval while it lasts.
        if (telegraphing && !wasTelegraphing) nextPulseTime = Time.time;
        if (telegraphing && Time.time >= nextPulseTime)
        {
            PlayOneShot(telegraphPulseClip, telegraphVolume);
            nextPulseTime = Time.time + pulseInterval;
        }

        // Telegraph resolves into the spawn switch-on; the free hum arrives with it.
        if (available && !wasAvailable && wasTelegraphing) PlayOneShot(spawnClip, spawnVolume);

        wasTelegraphing = telegraphing;
        wasAvailable = available;

        // Position: the holder's car when held, else the Crown itself.
        Transform holderView = held ? ResolveHolder(match) : null;
        Vector3 pos = holderView != null ? holderView.position : crown.transform.position;
        emitter.position = pos + Vector3.up * emitterHeight;

        // Hum: free <-> held crossfade (both run in sync, so the switch is seamless).
        bool free = available && !held;
        FadeLoop(humFreeSource, ref freeLevel, free ? humFreeVolume : 0f, dt);
        FadeLoop(humHeldSource, ref heldLevel, held ? humHeldVolume : 0f, dt);

        // Streak: 0..+5 semitones over 30 s of unbroken possession, from the replicated hold ticks.
        float target = 0f;
        if (held && cachedHolderState != null && cachedHolderState.HasState && match.Runner != null)
        {
            float seconds = cachedHolderState.CurrentHoldTicks / (float)match.Runner.TickRate;
            target = maxStreakSemitones * Mathf.Clamp01(seconds / streakSecondsToMax);
        }
        // Up: follow smoothly. Down (new holder, streak reset): also smooth, never a jump.
        semitones = Mathf.MoveTowards(semitones, target, pitchSlew * dt);
        float pitch = Mathf.Pow(2f, semitones / 12f);
        if (humHeldSource != null) humHeldSource.pitch = pitch;
        if (humFreeSource != null) humFreeSource.pitch = 1f;
    }

    // Holder's smoothed visual (View), cached until the Crown changes hands.
    Transform ResolveHolder(CrownMatchState match)
    {
        if (match.HolderId == cachedHolderId && cachedHolderView != null) return cachedHolderView;

        cachedHolderId = match.HolderId;
        cachedHolderView = null;
        cachedHolderState = null;
        if (match.Runner != null && match.Runner.TryFindObject(cachedHolderId, out NetworkObject holder) && holder != null)
        {
            var view = holder.transform.Find("View");
            cachedHolderView = view != null ? view : holder.transform;
            cachedHolderState = holder.GetComponent<PlayerMatchState>();
        }
        return cachedHolderView;
    }

    // Full volume to 5 m, gentle fall to the arena width, then held at the floor (never silent).
    AnimationCurve BuildRolloff()
    {
        float start = Mathf.Clamp01(fullVolumeDistance / arenaWidth);
        var curve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(start, 1f),
            new Keyframe(Mathf.Lerp(start, 1f, 0.25f), Mathf.Lerp(farFloor, 1f, 0.55f)),
            new Keyframe(Mathf.Lerp(start, 1f, 0.6f), Mathf.Lerp(farFloor, 1f, 0.2f)),
            new Keyframe(1f, farFloor));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    void Setup3D(AudioSource s, AnimationCurve rolloff)
    {
        if (s == null) return;
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.priority = 8;            // the Crown must never be culled
        s.maxDistance = arenaWidth;
        s.rolloffMode = AudioRolloffMode.Custom;
        s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
    }

    void SetupLoop(AudioSource s, AudioClip clip, AnimationCurve rolloff)
    {
        Setup3D(s, rolloff);
        if (s == null) return;
        s.clip = clip;
        s.loop = true;
        s.volume = 0f;
    }

    void FadeLoop(AudioSource s, ref float level, float target, float dt)
    {
        if (s == null || s.clip == null) return;
        level = Mathf.MoveTowards(level, target, dt / loopFadeSeconds);
        s.volume = level;
        if (level > 0.0001f) { if (!s.isPlaying) s.Play(); }
        else if (s.isPlaying) s.Stop();
    }

    void PlayOneShot(AudioClip clip, float volume)
    {
        if (oneShotSource != null && clip != null) oneShotSource.PlayOneShot(clip, volume);
    }
}
