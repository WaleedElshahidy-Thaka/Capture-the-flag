using UnityEngine;

// Match feedback sounds (2D, non-positional): 3-2-1 countdown, match start, final 10 seconds,
// match end, standings reveal (victory / defeat) and the superlative cards.
//
// MULTIPLAYER: MatchTimerState is the single source of truth (host-ticked, replicated to every peer).
// The countdown, start and final-10 moments have no events of their own, so this watches the replicated
// whole-second value and plays only when it CHANGES - once per client, in step with the real timer, and
// never again on a UI refresh. The end/standings/superlatives come from MatchTimerState.MatchFinished,
// which is raised exactly once per peer. A late joiner adopts the current state silently (no replay of
// beeps that already passed). Read-only: no networked variables, no RPCs, no gameplay writes.
//
// Put it on a scene object (e.g. "MatchAudio"). Plain AudioSources follow the SFX volume slider
// (GameAudioSettings drives AudioListener.volume when there is no mixer).
public class MatchAudioController : MonoBehaviour
{
    [Header("Sources (2D)")]
    [Tooltip("Countdown, start, end, standings, superlatives.")]
    [SerializeField] AudioSource uiSource;
    [Tooltip("Final-10 ticks (separate, so the match end can cut them).")]
    [SerializeField] AudioSource tickSource;
    [Tooltip("Superlative chimes (separate: their per-card pitch must not bend the standings reveal).")]
    [SerializeField] AudioSource rewardSource;

    [Header("Countdown")]
    [SerializeField] AudioClip countdown3;
    [SerializeField] AudioClip countdown2;
    [SerializeField] AudioClip countdown1;
    [SerializeField, Range(0f, 1f)] float countdownVolume = 0.9f;

    [Header("Match start")]
    [SerializeField] AudioClip matchStartClip;
    [SerializeField, Range(0f, 1f)] float matchStartVolume = 1f;

    [Header("Final 10 seconds")]
    [SerializeField] AudioClip finalTickClip;
    [Tooltip("Used for the last few seconds (see Urgent From).")]
    [SerializeField] AudioClip finalTickUrgentClip;
    [SerializeField, Range(1, 10)] int finalSeconds = 10;
    [SerializeField, Range(1, 10)] int urgentFrom = 3;
    [Tooltip("Pitch added per second as the clock runs down (tighter, more urgent).")]
    [SerializeField, Range(0f, 0.1f)] float tickPitchStep = 0.03f;
    [SerializeField, Range(0f, 1f)] float tickVolume = 0.8f;

    [Header("Match end")]
    [SerializeField] AudioClip matchEndClip;
    [SerializeField, Range(0f, 1f)] float matchEndVolume = 1f;

    [Header("Standings reveal (local player only)")]
    [SerializeField] AudioClip victoryClip;
    [SerializeField] AudioClip defeatClip;
    [SerializeField, Range(0f, 1f)] float standingsVolume = 0.9f;
    [Tooltip("After the results screen opens (ResultsUI fades in over 0.4 s), so it doesn't collide with the end tone.")]
    [SerializeField, Min(0f)] float standingsDelay = 0.45f;

    [Header("Superlatives")]
    [SerializeField] AudioClip superlativeClip;
    [SerializeField, Range(0f, 1f)] float superlativeVolume = 0.6f;
    [Tooltip("When the superlative cards pop in, after the results screen opens.")]
    [SerializeField, Min(0f)] float superlativeDelay = 0.9f;
    [SerializeField, Min(0f)] float superlativeStagger = 0.12f;
    [Tooltip("Pitch per card, rising, so the sequence never repeats the same note.")]
    [SerializeField] float[] superlativePitches = { 1f, 1.122f, 1.26f, 1.498f };

    MatchTimerState tracked;
    int lastCountdown;
    bool startPlayed;
    int lastSecond;
    float standingsAt = -1f;
    bool standingsWon;
    float superlativesAt = -1f;
    int superlativesTotal, superlativesPlayed;

    void Awake()
    {
        Configure2D(uiSource);
        Configure2D(tickSource);
        Configure2D(rewardSource);
    }

    void OnEnable() => MatchTimerState.MatchFinished += OnMatchFinished;
    void OnDisable() => MatchTimerState.MatchFinished -= OnMatchFinished;

    void Update()
    {
        UpdateScheduled();

        var timer = MatchTimerState.Instance;
        if (timer != tracked)
        {
            tracked = timer;
            Adopt(timer);
        }
        if (timer == null || timer.Object == null || !timer.Object.IsValid || timer.Finished) return;

        // 3-2-1: play when the whole second changes.
        if (timer.InCountdown)
        {
            int n = Mathf.CeilToInt(timer.CountdownSecondsRemaining);
            if (n != lastCountdown && n >= 1 && n <= 3) PlayCountdown(n);
            lastCountdown = n;
            return;
        }

        // Countdown over: robots unlock and the clock starts.
        if (!MatchTimerState.AllowsDriving) return;
        if (!startPlayed)
        {
            startPlayed = true;
            PlayMatchStart();
        }

        // Final seconds: one tick per whole second, only on the way down.
        int s = Mathf.CeilToInt(timer.SecondsRemaining);
        if (s < lastSecond && s >= 1 && s <= finalSeconds) PlayFinalTick(s);
        lastSecond = s;
    }

    // New timer (match spawned, or joined mid-match): take its current state without playing anything
    // that already happened.
    void Adopt(MatchTimerState timer)
    {
        standingsAt = superlativesAt = -1f;
        if (timer == null || timer.Object == null || !timer.Object.IsValid)
        {
            lastCountdown = 4;
            startPlayed = false;
            lastSecond = int.MaxValue;
            return;
        }

        bool running = MatchTimerState.AllowsDriving;
        lastCountdown = timer.InCountdown ? Mathf.CeilToInt(timer.CountdownSecondsRemaining) : (running ? 0 : 4);
        startPlayed = running;
        lastSecond = running ? Mathf.CeilToInt(timer.SecondsRemaining) : int.MaxValue;
    }

    void OnMatchFinished(MatchResult result)
    {
        PlayMatchEnd();
        if (result == null) return;

        standingsWon = result.localWon || result.isDraw;
        standingsAt = Time.unscaledTime + standingsDelay;

        superlativesTotal = 0;
        if (result.longestHoldIndex >= 0) superlativesTotal++;
        if (result.mostStealsIndex >= 0) superlativesTotal++;
        if (result.mostSlamsIndex >= 0) superlativesTotal++;
        if (result.mostDistanceIndex >= 0) superlativesTotal++;
        superlativesPlayed = 0;
        superlativesAt = superlativesTotal > 0 ? Time.unscaledTime + superlativeDelay : -1f;
    }

    void UpdateScheduled()
    {
        float now = Time.unscaledTime;
        if (standingsAt >= 0f && now >= standingsAt)
        {
            standingsAt = -1f;
            PlayStandingsReveal(standingsWon);
        }
        if (superlativesAt >= 0f && now >= superlativesAt)
        {
            PlaySuperlativeReveal(superlativesPlayed);
            superlativesPlayed++;
            superlativesAt = superlativesPlayed < superlativesTotal ? now + superlativeStagger : -1f;
        }
    }

    // ---------------- public API ----------------

    public void PlayCountdown(int number)
    {
        AudioClip clip = number == 3 ? countdown3 : number == 2 ? countdown2 : countdown1;
        Play(uiSource, clip, countdownVolume, 1f);
    }

    public void PlayMatchStart() => Play(uiSource, matchStartClip, matchStartVolume, 1f);

    public void PlayFinalTick(int secondsLeft)
    {
        bool urgent = secondsLeft <= urgentFrom && finalTickUrgentClip != null;
        float pitch = 1f + (finalSeconds - secondsLeft) * tickPitchStep;
        Play(tickSource, urgent ? finalTickUrgentClip : finalTickClip, tickVolume, pitch);
    }

    // Cuts the competing match audio (ticks, a beep still ringing) before the long ending tone.
    public void PlayMatchEnd()
    {
        if (tickSource != null) tickSource.Stop();
        if (uiSource != null) uiSource.Stop();
        if (rewardSource != null) rewardSource.Stop();
        Play(uiSource, matchEndClip, matchEndVolume, 1f);
    }

    public void PlayStandingsReveal(bool isWinner) =>
        Play(uiSource, isWinner ? victoryClip : defeatClip, standingsVolume, 1f);

    public void PlaySuperlativeReveal(int index)
    {
        float pitch = superlativePitches != null && superlativePitches.Length > 0
            ? superlativePitches[Mathf.Clamp(index, 0, superlativePitches.Length - 1)]
            : 1f;
        Play(rewardSource, superlativeClip, superlativeVolume, pitch);
    }

    // ---------------- plumbing ----------------

    static void Play(AudioSource source, AudioClip clip, float volume, float pitch)
    {
        if (source == null || clip == null) return;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }

    static void Configure2D(AudioSource s)
    {
        if (s == null) return;
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;   // gameplay feedback, not a sound in the arena
        s.priority = 16;       // stays audible over arena audio
        s.dopplerLevel = 0f;
    }
}
