using System.Collections.Generic;
using UnityEngine;

// All robot driving sounds for one car: motors, reverse, wheel scrub, hop and landings, drift slide,
// reserve-charge tone, boost stages, robot and wall collisions, wall scrape, spin-out, oil.
//
// MULTIPLAYER: PlayerMovement.LastDriftEvents, the drive model's speed/grounded/surface and the wall and
// car contact callbacks only exist inside the simulation (host + rollbacks on the predicting client) -
// remote robots never run it. So this component reads only NETWORKED state (drift phase, reserve, boost
// tick/multiplier, spin-out, steer) and each robot's VISIBLE motion, once per rendered frame, and reacts
// to changes. Every client hears every event exactly once, rollbacks can't double-fire it, and no
// networked data is added. Audio only: it never writes gameplay or network state.
//
// Put it on the car root. Loops live on child objects (filters are per GameObject); one-shots use a pool.
public class RobotAudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlayerMovement movement;
    [SerializeField] PlayerMatchState player;
    [Tooltip("The smoothed visual (View) - its motion drives speed, slip and direction.")]
    [SerializeField] Transform view;
    [Tooltip("The car body collider (robot contacts, wall probes).")]
    [SerializeField] BoxCollider body;

    [Header("Loop sources (one GameObject each)")]
    [SerializeField] AudioSource motorSource;
    [SerializeField] AudioHighPassFilter motorHighPass;
    [SerializeField] AudioSource reverseSource;
    [SerializeField] AudioSource slideSource;
    [SerializeField] AudioLowPassFilter slideLowPass;
    [SerializeField] AudioHighPassFilter slideHighPass;
    [SerializeField] AudioSource chargeSource;
    [SerializeField] AudioSource scrapeSource;
    [Tooltip("Pool for one-shots (layering: a new sound never cuts one that's playing).")]
    [SerializeField] AudioSource[] oneShotSources;

    [Header("Clips - loops")]
    [SerializeField] AudioClip motorLoop;
    [SerializeField] AudioClip reverseLoop;
    [SerializeField] AudioClip slideLoop;
    [SerializeField] AudioClip chargeLoop;
    [SerializeField] AudioClip scrapeLoop;

    [Header("Clips - one-shots (variations)")]
    [SerializeField] AudioClip[] hopClips;
    [SerializeField] AudioClip[] landNeutralClips;
    [SerializeField] AudioClip[] landSteerClips;
    [SerializeField] AudioClip[] scrubClips;
    [Tooltip("Stage 1, 2, 3.")]
    [SerializeField] AudioClip[] boostStageClips = new AudioClip[3];
    [SerializeField] AudioClip[] impactClips;
    [SerializeField] AudioClip[] crunchClips;
    [SerializeField] AudioClip[] wallImpactClips;
    [SerializeField] AudioClip[] spinOutClips;
    [SerializeField] AudioClip[] oilCatchClips;

    [Header("Spatial")]
    [SerializeField, Min(0f)] float minDistance = 2f;
    [SerializeField, Min(0.1f)] float maxDistance = 40f;
    [Tooltip("Your own robot: mostly non-positional so it's always clear.")]
    [SerializeField, Range(0f, 1f)] float localSpatialBlend = 0.3f;
    [SerializeField, Range(0f, 1f)] float localVolume = 1f;
    [SerializeField, Range(0f, 1f)] float remoteVolume = 0.85f;

    [Header("General")]
    [SerializeField, Min(0.01f)] float loopFadeSeconds = 0.1f;
    [Tooltip("Random pitch +/- on one-shots (0.05 = 5%).")]
    [SerializeField, Range(0f, 0.2f)] float pitchVariation = 0.05f;
    [Tooltip("How quickly the measured speed follows the robot (higher = snappier).")]
    [SerializeField, Min(1f)] float speedSmoothing = 10f;

    [Header("Motor (pitch/volume vs speed / TopSpeed)")]
    [SerializeField] float idlePitch = 0.55f;
    [SerializeField] float topSpeedPitch = 1.25f;
    [Tooltip("Extra pitch per 1.0 of speed above TopSpeed (boosting).")]
    [SerializeField] float overspeedPitch = 1.0f;
    [SerializeField] float maxPitch = 2.2f;
    [SerializeField, Range(0f, 1f)] float idleVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] float topSpeedVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] float maxMotorVolume = 0.95f;

    [Header("Reverse")]
    [Tooltip("Backward speed (m/s) at which the reverse loop takes over.")]
    [SerializeField, Min(0f)] float reverseThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] float reverseVolume = 0.6f;
    [SerializeField] Vector2 reversePitch = new Vector2(0.7f, 1.1f);

    [Header("Wheel scrub (hard turn, not drifting)")]
    [SerializeField, Range(0f, 1f)] float scrubSteer = 0.85f;
    [SerializeField, Min(0f)] float scrubMinSpeed = 10f;
    [SerializeField, Min(0f)] float scrubCooldown = 0.6f;
    [SerializeField, Range(0f, 1f)] float scrubVolume = 0.55f;

    [Header("Drift slide (follows slip angle)")]
    [SerializeField, Range(0f, 1f)] float slideVolume = 0.8f;
    [Tooltip("Slip angle (deg) for full volume/brightness.")]
    [SerializeField, Min(1f)] float slipForFull = 30f;
    [Tooltip("Low-pass cutoff at low / full slip (brighter with slip).")]
    [SerializeField] Vector2 slideLowPassRange = new Vector2(2000f, 9000f);
    [Tooltip("High-pass cutoff at low / full slip (thinner as the arc widens and slip drops).")]
    [SerializeField] Vector2 slideHighPassRange = new Vector2(900f, 120f);

    [Header("Reserve charge tone")]
    [SerializeField, Range(0f, 1f)] float chargeVolume = 0.35f;
    [Tooltip("Pitch at the start / end of each stage's climb - resets at every stage.")]
    [SerializeField] Vector2 chargePitch = new Vector2(0.8f, 1.6f);
    [Tooltip("Each later stage starts this much higher, so stages are recognisable by ear.")]
    [SerializeField, Min(1f)] float stagePitchStep = 1.12f;

    [Header("Hop / landing")]
    [SerializeField, Range(0f, 1f)] float hopVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float landVolume = 0.85f;
    [Tooltip("Ignore the same event again within this time (prediction corrections).")]
    [SerializeField, Min(0f)] float eventGuardSeconds = 0.15f;

    [Header("Boost stages")]
    [SerializeField] float[] boostStageVolumes = { 0.75f, 0.9f, 1f };

    [Header("Robot collision")]
    [SerializeField, Min(0f)] float minClosingSpeed = 1.5f;
    [SerializeField, Min(0.1f)] float fullClosingSpeed = 15f;
    [Tooltip("Body gap (m) counted as touching.")]
    [SerializeField, Min(0f)] float contactGap = 0.08f;
    [SerializeField, Range(0f, 1f)] float crunchVolume = 0.7f;
    [Tooltip("Heavier hits play lower (pitch drop at full closing speed).")]
    [SerializeField, Range(0f, 0.5f)] float heavyPitchDrop = 0.15f;

    [Header("Walls")]
    [SerializeField] LayerMask wallMask = ~0;
    [SerializeField, Min(0f)] float wallProbe = 0.2f;
    [SerializeField, Min(0f)] float wallImpactMinClosing = 3f;
    [SerializeField, Min(0.1f)] float wallFullClosing = 15f;
    [SerializeField, Min(0f)] float wallImpactCooldown = 0.3f;
    [SerializeField, Min(0f)] float scrapeMinSpeed = 2f;
    [SerializeField, Min(0.1f)] float scrapeFullSpeed = 15f;
    [SerializeField, Range(0f, 1f)] float scrapeVolume = 0.6f;

    [Header("Spin-out")]
    [SerializeField, Range(0f, 1f)] float spinOutVolume = 0.9f;
    [Tooltip("Motor pitch jump when grip disappears; fades as control returns.")]
    [SerializeField] float spinMotorRace = 0.6f;
    [SerializeField, Min(0.1f)] float spinRaceSeconds = 0.8f;

    [Header("Oil")]
    [Tooltip("Motor high-pass on oil: the drive loop turns thin and slick.")]
    [SerializeField] float oilMotorHighPass = 900f;
    [SerializeField, Range(0f, 1f)] float oilMotorVolume = 0.75f;
    [Tooltip("Slide grit level on oil (grit drops away).")]
    [SerializeField, Range(0f, 1f)] float oilSlideGrit = 0.4f;
    [SerializeField, Range(0f, 1f)] float oilCatchVolume = 0.7f;

    [Header("Duck under the Crown steal stinger")]
    [SerializeField] float duckDecibels = -6f;
    [SerializeField, Min(0f)] float duckSeconds = 0.15f;

    // ---- shared across robots ----
    const int MaxContactVoices = 8;
    static readonly List<RobotAudioController> all = new List<RobotAudioController>();
    static readonly float[] contactVoiceEnds = new float[MaxContactVoices];
    static float duckUntil;

    // ---- per robot ----
    readonly HashSet<RobotAudioController> touching = new HashSet<RobotAudioController>();
    readonly RaycastHit[] hits = new RaycastHit[6];
    readonly int[] lastVariation = new int[10];

    float motorLevel, reverseLevel, slideLevel, chargeLevel, scrapeLevel;
    Vector3 lastPosition, velocity;
    bool hasLastPosition, initialized, isLocal, localityApplied;
    int lastPhase, lastBoostTick;
    bool lastOverdrive, lastSpinning, lastOnOil, wasHittingWall;
    float nextScrubTime, nextWallImpactTime, spinRaceLeft, motorHighPassCutoff = 10f;
    readonly float[] lastEventTime = { -10f, -10f, -10f };
    int nextOneShot;

    /// <summary>Visible (smoothed) velocity, for the other robots' contact maths.</summary>
    public Vector3 VisibleVelocity => velocity;

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (player == null) player = GetComponent<PlayerMatchState>();
        if (body == null) body = GetComponent<BoxCollider>();
        if (view == null) view = transform;

        SetupLoop(motorSource, motorLoop);
        SetupLoop(reverseSource, reverseLoop);
        SetupLoop(slideSource, slideLoop);
        SetupLoop(chargeSource, chargeLoop);
        SetupLoop(scrapeSource, scrapeLoop);
        if (oneShotSources != null)
            foreach (var s in oneShotSources) if (s != null) { s.playOnAwake = false; s.loop = false; }
    }

    void OnEnable()
    {
        all.Add(this);
        CrownAudio.StealStingerPlayed += OnStealStinger;
    }

    void OnDisable()
    {
        all.Remove(this);
        CrownAudio.StealStingerPlayed -= OnStealStinger;
        touching.Clear();
        StopLoop(motorSource, ref motorLevel);
        StopLoop(reverseSource, ref reverseLevel);
        StopLoop(slideSource, ref slideLevel);
        StopLoop(chargeSource, ref chargeLevel);
        StopLoop(scrapeSource, ref scrapeLevel);
        initialized = false;
        hasLastPosition = false;
    }

    void OnStealStinger() => duckUntil = Time.time + duckSeconds;

    // Robot sounds only during play: after the 3-2-1 countdown (the same gate that lets cars drive),
    // until the match is finished.
    static bool MatchIsLive() =>
        MatchTimerState.AllowsDriving && !MatchTimerState.Instance.Finished;

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (movement == null || movement.Object == null || !movement.Object.IsValid || !MatchIsLive())
        {
            // Lobby, intro, 3-2-1 countdown or match over: silent. Re-adopt the state when play starts,
            // so nothing that happened before "GO" fires a sound.
            FadeAll(dt);
            initialized = false;
            hasLastPosition = false;
            velocity = Vector3.zero;
            touching.Clear();
            return;
        }

        var h = HandlingValuesProvider.Current;
        UpdateLocality();
        UpdateVelocity(dt);

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        float speed = flat.magnitude;
        Vector3 fwd = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        float forwardSpeed = Vector3.Dot(flat, fwd);

        int phase = (int)movement.CurrentDriftPhase;
        bool spinning = movement.IsSpinningOut;
        bool boosting = movement.IsBoosting;
        bool overdrive = movement.IsOverdriveActive;
        int boostTick = movement.LastBoostTick;
        bool onOil = movement.Runner != null && OilSurfaceRegistry.IsOilAt(movement.Runner, view.position);

        if (!initialized)
        {
            // First valid frame (spawn / join mid-match): adopt the current state, play nothing.
            initialized = true;
        }
        else
        {
            DetectEvents(phase, boosting, overdrive, boostTick, spinning, onOil, speed, h);
        }

        lastPhase = phase;
        lastBoostTick = boostTick;
        lastOverdrive = overdrive;
        lastSpinning = spinning;
        lastOnOil = onOil;

        float duck = Time.time < duckUntil ? Mathf.Pow(10f, duckDecibels / 20f) : 1f;
        float scale = (isLocal ? localVolume : remoteVolume) * duck;

        UpdateMotor(forwardSpeed, onOil, h, scale, dt);
        UpdateSlide(phase, flat, fwd, speed, onOil, scale, dt);
        UpdateCharge(phase, h, scale, dt);
        UpdateWalls(flat, speed, scale, dt);
        UpdateRobotContacts();
    }

    // ---------------- events ----------------

    void DetectEvents(int phase, bool boosting, bool overdrive, int boostTick, bool spinning, bool onOil, float speed, HandlingValues h)
    {
        const int Neutral = 0, Hop = 1, Sliding = 2;

        if (phase != lastPhase)
        {
            if (lastPhase == Neutral && phase == Hop) GuardedOneShot(0, hopClips, hopVolume, 2);
            else if (lastPhase == Hop && phase == Sliding) GuardedOneShot(1, landSteerClips, landVolume, 3);
            else if (lastPhase == Hop && phase == Neutral) GuardedOneShot(1, landNeutralClips, landVolume, 4);
        }

        // A new boost (stage, chain link). Overdrive's own start is BoostAudio's job.
        if (boostTick != lastBoostTick && boosting && !(overdrive && !lastOverdrive))
        {
            int stage = StageFromMultiplier(movement.BoostMultiplierValue, h);
            var clip = boostStageClips != null && stage - 1 < boostStageClips.Length ? boostStageClips[stage - 1] : null;
            float vol = boostStageVolumes != null && stage - 1 < boostStageVolumes.Length ? boostStageVolumes[stage - 1] : 1f;
            PlayClip(clip, vol, 1f);    // layered: never cuts a boost that's still sounding
        }

        if (spinning && !lastSpinning)
        {
            PlayVariation(spinOutClips, spinOutVolume, 1f, 5);
            spinRaceLeft = spinRaceSeconds;
        }

        if (lastOnOil && !onOil && speed > 1f)
            PlayVariation(oilCatchClips, oilCatchVolume, 1f, 6);

        if (phase != Sliding && !spinning && !onOil && speed >= scrubMinSpeed
            && Mathf.Abs(movement.SteerValue) >= scrubSteer && Time.time >= nextScrubTime)
        {
            nextScrubTime = Time.time + scrubCooldown;
            PlayVariation(scrubClips, scrubVolume, 1f, 7);
        }
    }

    void GuardedOneShot(int guardIndex, AudioClip[] clips, float volume, int variationSlot)
    {
        if (Time.time - lastEventTime[guardIndex] < eventGuardSeconds) return;
        lastEventTime[guardIndex] = Time.time;
        PlayVariation(clips, volume, 1f, variationSlot);
    }

    static int StageFromMultiplier(float multiplier, HandlingValues h)
    {
        float d1 = Mathf.Abs(multiplier - h.Stage1BoostMultiplier);
        float d2 = Mathf.Abs(multiplier - h.Stage2BoostMultiplier);
        float d3 = Mathf.Abs(multiplier - h.Stage3BoostMultiplier);
        return d3 <= d2 && d3 <= d1 ? 3 : d2 <= d1 ? 2 : 1;
    }

    // ---------------- loops ----------------

    void UpdateMotor(float forwardSpeed, bool onOil, HandlingValues h, float scale, float dt)
    {
        bool reversing = forwardSpeed < -reverseThreshold;
        float s = Mathf.Abs(forwardSpeed) / Mathf.Max(0.1f, h.TopSpeed);

        spinRaceLeft = Mathf.Max(0f, spinRaceLeft - dt);
        float race = spinMotorRace * (spinRaceLeft / Mathf.Max(0.01f, spinRaceSeconds));

        float pitch = Mathf.Lerp(idlePitch, topSpeedPitch, Mathf.Clamp01(s)) + Mathf.Max(0f, s - 1f) * overspeedPitch + race;
        float vol = Mathf.Min(maxMotorVolume, Mathf.Lerp(idleVolume, topSpeedVolume, Mathf.Clamp01(s)) + Mathf.Max(0f, s - 1f) * 0.5f);
        if (onOil) vol *= oilMotorVolume;

        motorHighPassCutoff = Mathf.MoveTowards(motorHighPassCutoff, onOil ? oilMotorHighPass : 10f, 8000f * dt);
        if (motorHighPass != null) motorHighPass.cutoffFrequency = motorHighPassCutoff;

        if (motorSource != null) motorSource.pitch = Mathf.Min(maxPitch, pitch);
        FadeLoop(motorSource, ref motorLevel, reversing ? 0f : vol * scale, dt);

        if (reverseSource != null)
            reverseSource.pitch = Mathf.Lerp(reversePitch.x, reversePitch.y, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / Mathf.Max(0.1f, h.ReverseTopSpeed)));
        FadeLoop(reverseSource, ref reverseLevel, reversing ? reverseVolume * scale : 0f, dt);
    }

    void UpdateSlide(int phase, Vector3 flat, Vector3 fwd, float speed, bool onOil, float scale, float dt)
    {
        bool sliding = phase == 2;
        float slip01 = 0f;
        if (sliding && speed > 0.5f)
        {
            float lateral = Mathf.Abs(Vector3.Dot(flat, Vector3.Cross(Vector3.up, fwd)));
            float along = Mathf.Abs(Vector3.Dot(flat, fwd));
            slip01 = Mathf.Clamp01(Mathf.Atan2(lateral, Mathf.Max(0.01f, along)) * Mathf.Rad2Deg / slipForFull);
        }

        if (slideLowPass != null) slideLowPass.cutoffFrequency = Mathf.Lerp(slideLowPassRange.x, slideLowPassRange.y, slip01) * (onOil ? 0.5f : 1f);
        if (slideHighPass != null) slideHighPass.cutoffFrequency = Mathf.Lerp(slideHighPassRange.x, slideHighPassRange.y, slip01);

        float vol = sliding ? Mathf.Lerp(0.35f, 1f, slip01) * slideVolume * (onOil ? oilSlideGrit : 1f) : 0f;
        FadeLoop(slideSource, ref slideLevel, vol * scale, dt);
    }

    // Rising tone through each stage; restarts low at every threshold (same maths as the drift meter).
    void UpdateCharge(int phase, HandlingValues h, float scale, float dt)
    {
        float reserve = Mathf.Max(0f, movement.DriftReserveValue);
        int stage = DriftBoost.StageFor(reserve, h);
        bool active = phase == 2 && stage < 3;

        if (active && chargeSource != null)
        {
            float from = Threshold(stage, h), to = Threshold(stage + 1, h);
            float progress = to > from ? Mathf.Clamp01((reserve - from) / (to - from)) : 0f;
            chargeSource.pitch = Mathf.Lerp(chargePitch.x, chargePitch.y, progress) * Mathf.Pow(stagePitchStep, stage);
        }
        FadeLoop(chargeSource, ref chargeLevel, active ? chargeVolume * scale : 0f, dt);
    }

    static float Threshold(int stage, HandlingValues h)
    {
        switch (stage)
        {
            case 0: return 0f;
            case 1: return h.Stage1Threshold;
            case 2: return h.Stage2Threshold;
            default: return h.Stage3Threshold;
        }
    }

    // Wall impact (one-shot, by closing speed) and scrape (loop, by speed along the wall).
    void UpdateWalls(Vector3 flat, float speed, float scale, float dt)
    {
        float scrapeTarget = 0f;
        bool hittingWall = false;

        if (speed > 1f && body != null)
        {
            Vector3 size = Vector3.Scale(body.size, body.transform.lossyScale) * 0.5f;
            Vector3 origin = view.position + Vector3.up * 0.35f;
            Vector3 dir = flat / speed;
            Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized);
            float reach = Mathf.Max(size.x, size.z) + Mathf.Max(wallProbe, speed * dt * 2f);

            if (ProbeWall(origin, dir, reach, out Vector3 n))
            {
                hittingWall = true;
                float closing = Vector3.Dot(flat, -n);
                if (!wasHittingWall && closing >= wallImpactMinClosing && Time.time >= nextWallImpactTime)
                {
                    nextWallImpactTime = Time.time + wallImpactCooldown;
                    PlayVariation(wallImpactClips, Mathf.InverseLerp(wallImpactMinClosing * 0.5f, wallFullClosing, closing), 1f, 8);
                }
                scrapeTarget = ScrapeLevel(flat, n);
            }

            if (ProbeWall(origin, right, size.x + wallProbe, out n) || ProbeWall(origin, -right, size.x + wallProbe, out n))
            {
                hittingWall = true;
                scrapeTarget = Mathf.Max(scrapeTarget, ScrapeLevel(flat, n));
            }
        }

        wasHittingWall = hittingWall;
        FadeLoop(scrapeSource, ref scrapeLevel, scrapeTarget * scrapeVolume * scale, dt);
    }

    float ScrapeLevel(Vector3 flat, Vector3 wallNormal)
    {
        float along = Vector3.ProjectOnPlane(flat, wallNormal).magnitude;
        return along < scrapeMinSpeed ? 0f : Mathf.InverseLerp(scrapeMinSpeed, scrapeFullSpeed, along);
    }

    // Nearest WALL hit (steep surface, no Rigidbody - so never a robot, including this one).
    bool ProbeWall(Vector3 origin, Vector3 dir, float distance, out Vector3 normal)
    {
        normal = default;
        int count = Physics.RaycastNonAlloc(origin, dir, hits, distance, wallMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider.attachedRigidbody != null || hit.normal.y >= 0.5f) continue;
            if (hit.distance < best) { best = hit.distance; normal = hit.normal; }
        }
        return best < float.MaxValue;
    }

    // ---------------- robot vs robot ----------------

    void UpdateRobotContacts()
    {
        if (body == null || movement.Object == null) return;
        uint myId = movement.Object.Id.Raw;
        Vector3 myPos = view.position;

        for (int i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == this || other.body == null || other.movement == null || other.movement.Object == null || !other.movement.Object.IsValid)
                continue;
            if (other.movement.Object.Id.Raw < myId) continue;               // each pair once: lower id plays

            Vector3 toOther = other.view.position - myPos;
            if (toOther.sqrMagnitude > 16f) { touching.Remove(other); continue; }   // > 4 m apart

            bool isTouching = SurfaceGap(body, other.body) <= contactGap;
            if (!isTouching) { touching.Remove(other); continue; }
            if (!touching.Add(other)) continue;                               // already in contact

            toOther.y = 0f;
            if (toOther.sqrMagnitude < 0.0001f) continue;
            Vector3 dir = toOther.normalized;
            float myApproach = Vector3.Dot(velocity, dir);
            float otherApproach = -Vector3.Dot(other.velocity, dir);
            float closing = myApproach + otherApproach;
            if (closing < minClosingSpeed || !TryTakeContactVoice(0.6f)) continue;

            float k = Mathf.InverseLerp(minClosingSpeed, fullClosingSpeed, closing);
            PlayVariation(impactClips, Mathf.Lerp(0.35f, 1f, k), 1f - heavyPitchDrop * k, 9);

            var aggressor = myApproach >= otherApproach ? movement : other.movement;
            if (aggressor.IsBoosting) PlayVariation(crunchClips, crunchVolume * Mathf.Lerp(0.5f, 1f, k), 1f, 9);
        }
    }

    static bool TryTakeContactVoice(float seconds)
    {
        float now = Time.time;
        for (int i = 0; i < MaxContactVoices; i++)
        {
            if (contactVoiceEnds[i] <= now)
            {
                contactVoiceEnds[i] = now + seconds;
                return true;
            }
        }
        return false;   // 8 contact sounds already playing
    }

    static float SurfaceGap(BoxCollider a, BoxCollider b)
    {
        Vector3 onB = b.ClosestPoint(a.bounds.center);
        Vector3 onA = a.ClosestPoint(onB);
        onB = b.ClosestPoint(onA);
        return Vector3.Distance(onA, onB);
    }

    // ---------------- plumbing ----------------

    void UpdateVelocity(float dt)
    {
        Vector3 pos = view.position;
        if (hasLastPosition)
        {
            Vector3 raw = (pos - lastPosition) / dt;
            if (raw.sqrMagnitude > 100f * 100f) raw = velocity;                // ignore teleports (respawn)
            velocity = Vector3.Lerp(velocity, raw, 1f - Mathf.Exp(-speedSmoothing * dt));
        }
        lastPosition = pos;
        hasLastPosition = true;
    }

    void UpdateLocality()
    {
        bool local = player != null && player == PlayerRoster.Local;
        if (localityApplied && local == isLocal) return;
        isLocal = local;
        localityApplied = true;

        float blend = isLocal ? localSpatialBlend : 1f;
        Configure(motorSource, blend); Configure(reverseSource, blend); Configure(slideSource, blend);
        Configure(chargeSource, blend); Configure(scrapeSource, blend);
        if (oneShotSources != null) foreach (var s in oneShotSources) Configure(s, blend);
    }

    void Configure(AudioSource s, float blend)
    {
        if (s == null) return;
        s.spatialBlend = blend;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        s.dopplerLevel = 0f;
    }

    static void SetupLoop(AudioSource s, AudioClip clip)
    {
        if (s == null) return;
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.volume = 0f;
    }

    // Fades a loop toward a volume; starts it only when it becomes audible, stops it when silent.
    void FadeLoop(AudioSource s, ref float level, float target, float dt)
    {
        if (s == null || s.clip == null) return;
        level = Mathf.MoveTowards(level, target, dt / loopFadeSeconds);
        s.volume = level;
        if (level > 0.0001f) { if (!s.isPlaying) s.Play(); }
        else if (s.isPlaying) s.Stop();
    }

    void FadeAll(float dt)
    {
        FadeLoop(motorSource, ref motorLevel, 0f, dt);
        FadeLoop(reverseSource, ref reverseLevel, 0f, dt);
        FadeLoop(slideSource, ref slideLevel, 0f, dt);
        FadeLoop(chargeSource, ref chargeLevel, 0f, dt);
        FadeLoop(scrapeSource, ref scrapeLevel, 0f, dt);
    }

    static void StopLoop(AudioSource s, ref float level)
    {
        level = 0f;
        if (s != null) { s.volume = 0f; if (s.isPlaying) s.Stop(); }
    }

    // Random variation, never the same one twice in a row.
    void PlayVariation(AudioClip[] clips, float volume, float pitch, int slot)
    {
        if (clips == null || clips.Length == 0) return;
        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == lastVariation[slot]) index = (index + 1) % clips.Length;
        lastVariation[slot] = index;
        PlayClip(clips[index], volume, pitch);
    }

    void PlayClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || oneShotSources == null || oneShotSources.Length == 0) return;
        var s = oneShotSources[nextOneShot];
        nextOneShot = (nextOneShot + 1) % oneShotSources.Length;
        if (s == null) return;
        s.pitch = pitch * (1f + Random.Range(-pitchVariation, pitchVariation));
        s.PlayOneShot(clip, Mathf.Clamp01(volume) * (isLocal ? localVolume : remoteVolume));
    }
}
