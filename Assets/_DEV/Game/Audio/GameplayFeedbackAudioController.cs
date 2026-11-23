using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

// Personal gameplay feedback (2D): what is happening to YOU, without looking at the HUD.
//
// Who hears what: everything here is about the LOCAL player's car (PlayerRoster.Local, the car this client
// has input authority over), so a sound meant for one player never plays for the others - each client
// only ever reacts to its own car. "Blocked" plays for both involved players because each of them detects
// their own involvement on their own client.
//
// MULTIPLAYER: GLOWTAG's gameplay raises no events for these moments; the facts live in replicated state
// (drift window stage, Score, HolderId, Slams, IsSpinningOut, HeldItem, power-up IsActive). This watches
// those values once per rendered frame and plays only when one CHANGES - one sound per real event, no
// spam from repeated updates, no networked variables added, no gameplay writes. The first live frame
// (match start, late join) adopts the current state silently.
//
// Approximations (no such gameplay events exist):
//   Blocked - your car touches the Crown holder while the Crown is protected or the holder's Damper is on
//             (the host decides blocks and only logs them).
//   Closing from behind - a rival within range and behind you while you hold the Crown (no rear-threat
//             system exists; PlayerIndicatorsUI only draws markers).
// Hit-stop does not exist in the project: the steal stinger plays on the frame the Crown changes hands.
public class GameplayFeedbackAudioController : MonoBehaviour
{
    [Header("Sources (2D)")]
    [Tooltip("Pool for ordinary feedback one-shots.")]
    [SerializeField] AudioSource[] pool;
    [Tooltip("Dedicated, top-priority source for the Crown steal stinger - never dropped by voice limits.")]
    [SerializeField] AudioSource stingerSource;
    [SerializeField] AudioSource threatLoopSource;

    [Header("Drift")]
    [SerializeField] AudioClip stageWindowClip;
    [SerializeField] AudioClip stageMissedClip;
    [SerializeField, Range(0f, 1f)] float stageWindowVolume = 0.8f;
    [Tooltip("Soft and neutral - never a failure sound. 0 = silent.")]
    [SerializeField, Range(0f, 1f)] float stageMissedVolume = 0.25f;
    [Tooltip("Pitch per stage window (1, 2, 3) so stages are recognisable.")]
    [SerializeField] float[] stagePitch = { 1f, 1.12f, 1.26f };

    [Header("Crown")]
    [SerializeField] AudioClip[] scoreTickClips;
    [SerializeField, Range(0f, 1f)] float scoreTickVolume = 0.3f;
    [SerializeField] AudioClip tookCrownClip;
    [SerializeField, Range(0f, 1f)] float tookCrownVolume = 1f;
    [SerializeField] AudioClip lostCrownClip;
    [SerializeField, Range(0f, 1f)] float lostCrownVolume = 0.6f;
    [SerializeField] AudioClip[] blockedClips;
    [SerializeField, Range(0f, 1f)] float blockedVolume = 0.85f;

    [Header("Collision / spin-out")]
    [SerializeField] AudioClip slamLandedClip;
    [SerializeField, Range(0f, 1f)] float slamLandedVolume = 0.9f;
    [SerializeField] AudioClip spunOutClip;
    [SerializeField, Range(0f, 1f)] float spunOutVolume = 0.7f;
    [Tooltip("Mix low-pass while your control is gone.")]
    [SerializeField] float muffleCutoff = 900f;
    [SerializeField, Min(0.01f)] float muffleInSeconds = 0.08f;
    [SerializeField, Min(0.01f)] float muffleOutSeconds = 0.35f;

    [Header("Crates / power-ups")]
    [SerializeField] AudioClip crateRevealClip;
    [SerializeField, Range(0f, 1f)] float crateRevealVolume = 0.75f;
    [Tooltip("Item-used confirm per PowerUpType: Overdrive, Grapple Field, Damper Field, Oil Slick, Repulse Pulse.")]
    [SerializeField] AudioClip[] itemUsedClips = new AudioClip[5];
    [SerializeField, Range(0f, 1f)] float itemUsedVolume = 0.5f;
    [SerializeField] AudioClip itemExpireClip;
    [SerializeField, Range(0f, 1f)] float itemExpireVolume = 0.55f;
    [SerializeField] AudioClip nopeClip;
    [SerializeField, Range(0f, 1f)] float nopeVolume = 0.45f;
    [SerializeField, Min(0f)] float nopeCooldown = 0.25f;

    [Header("Threat / lead")]
    [SerializeField] AudioClip rearThreatLoop;
    [SerializeField, Min(1f)] float threatRange = 15f;
    [Tooltip("How far behind counts (-1 = straight behind only, 0 = anywhere in the rear half).")]
    [SerializeField, Range(-1f, 0.5f)] float rearDot = -0.2f;
    [SerializeField, Range(0f, 1f)] float threatMaxVolume = 0.45f;
    [SerializeField] Vector2 threatPitch = new Vector2(0.9f, 1.15f);
    [SerializeField, Min(0.01f)] float threatFadeSeconds = 0.25f;
    [SerializeField] AudioClip tookLeadClip;
    [SerializeField, Range(0f, 1f)] float tookLeadVolume = 0.65f;

    [Header("Blocked detection")]
    [Tooltip("Body gap (m) counted as a hit.")]
    [SerializeField, Min(0f)] float blockContactGap = 0.1f;
    [SerializeField, Min(0f)] float blockCooldown = 0.5f;

    [Header("General")]
    [SerializeField, Range(0f, 0.2f)] float pitchVariation = 0.05f;

    // ---- local car (re-cached when the local player changes) ----
    PlayerMatchState local;
    PlayerMovement movement;
    PowerUpInventory inventory;
    DamperField damper;
    GrappleField grapple;
    BoxCollider body;
    Transform view;
    RobotAudioController robot;

    // ---- last seen state ----
    bool initialized, touchingHolder;
    int windowStage, boostTick, score, slams;
    NetworkId holder, leader;
    bool spinning, damperOn, grappleOn, overdriveOn;
    PowerUpType heldItem;
    float nextNope, nextBlock, muffle, threatLevel;
    int nextPool, lastScoreClip = -1, lastBlockClip = -1;
    AudioLowPassFilter listenerFilter;

    void Awake()
    {
        if (pool != null) foreach (var s in pool) Configure2D(s, 64);
        Configure2D(stingerSource, 0);
        Configure2D(threatLoopSource, 32);
        if (threatLoopSource != null) { threatLoopSource.clip = rearThreatLoop; threatLoopSource.loop = true; threatLoopSource.volume = 0f; }
    }

    void OnDisable() => SetMuffle(0f);

    void Update()
    {
        float dt = Time.deltaTime;
        var crown = CrownMatchState.Instance;

        if (!ResolveLocal() || !MatchIsLive() || crown == null || crown.Object == null || !crown.Object.IsValid)
        {
            initialized = false;
            FadeThreat(0f, 1f, dt);
            UpdateMuffle(false, dt);
            return;
        }

        NetworkId me = local.Object.Id;
        int nowWindow = movement.DriftWindowStage;
        int nowBoost = movement.LastBoostTick;
        int nowScore = local.Score;
        int nowSlams = local.Slams;
        NetworkId nowHolder = crown.HolderId;
        bool nowSpinning = movement.IsSpinningOut;
        PowerUpType nowItem = inventory != null ? inventory.HeldItem : PowerUpType.None;
        bool nowDamper = damper != null && damper.IsActive;
        bool nowGrapple = grapple != null && grapple.IsActive;
        bool nowOverdrive = movement.IsOverdriveActive;
        NetworkId nowLeader = Leader();

        if (!initialized)
        {
            initialized = true;     // adopt silently
        }
        else
        {
            // Drift: window opens / closes unused.
            if (nowWindow > 0 && nowWindow != windowStage)
                Play(stageWindowClip, stageWindowVolume, stagePitch[Mathf.Clamp(nowWindow - 1, 0, stagePitch.Length - 1)]);
            else if (nowWindow == 0 && windowStage > 0 && nowBoost == boostTick && stageMissedVolume > 0f)
                Play(stageMissedClip, stageMissedVolume, 1f);

            // Crown: steal / loss (pickups of a free Crown are CrownAudio's).
            if (nowHolder != holder)
            {
                if (nowHolder == me && holder != default) PlayStinger();
                else if (holder == me && nowHolder != default) Play(lostCrownClip, lostCrownVolume, 1f);
            }

            // Score tick while you hold it.
            if (nowScore > score && nowHolder == me)
                PlayVariation(scoreTickClips, ref lastScoreClip, scoreTickVolume);

            // Slam landed (the host counts your slams), spun out (muffle while control is gone).
            if (nowSlams > slams) Play(slamLandedClip, slamLandedVolume, 1f);
            if (nowSpinning && !spinning) Play(spunOutClip, spunOutVolume, 1f);

            // Items: crate settles on an item, item used, effect ended.
            if (nowItem != heldItem)
            {
                if (heldItem == PowerUpType.None && nowItem != PowerUpType.None) Play(crateRevealClip, crateRevealVolume, 1f);
                else if (heldItem != PowerUpType.None && nowItem == PowerUpType.None) PlayItemUsed(heldItem);
            }
            bool justTookCrown = nowHolder == me && holder != me;
            if ((damperOn && !nowDamper) || (overdriveOn && !nowOverdrive) || (grappleOn && !nowGrapple && !justTookCrown))
                Play(itemExpireClip, itemExpireVolume, 1f);

            // Took the lead.
            if (nowLeader == me && leader != me && nowLeader != default) Play(tookLeadClip, tookLeadVolume, 1f);
        }

        windowStage = nowWindow; boostTick = nowBoost; score = nowScore; slams = nowSlams;
        holder = nowHolder; spinning = nowSpinning; heldItem = nowItem;
        damperOn = nowDamper; grappleOn = nowGrapple; overdriveOn = nowOverdrive; leader = nowLeader;

        CheckNothingToUse(nowItem);
        CheckBlocked(crown, me);
        UpdateRearThreat(nowHolder == me, dt);
        UpdateMuffle(nowSpinning, dt);
    }

    // ---------------- detection ----------------

    bool ResolveLocal()
    {
        var current = PlayerRoster.Local;
        if (current != local)
        {
            local = current;
            movement = local != null ? local.GetComponent<PlayerMovement>() : null;
            inventory = local != null ? local.GetComponent<PowerUpInventory>() : null;
            damper = local != null ? local.GetComponent<DamperField>() : null;
            grapple = local != null ? local.GetComponent<GrappleField>() : null;
            body = local != null ? local.GetComponent<BoxCollider>() : null;
            robot = local != null ? local.GetComponent<RobotAudioController>() : null;
            view = null;
            if (local != null)
            {
                view = local.transform.Find("View");
                if (view == null) view = local.transform;
            }
            initialized = false;
        }
        return local != null && local.HasState && movement != null && local.Object != null && local.Object.IsValid;
    }

    static bool MatchIsLive() => MatchTimerState.AllowsDriving && !MatchTimerState.Instance.Finished;

    // Strict top score (a tie isn't a lead) - the same rule as MatchEventsUI.
    static NetworkId Leader()
    {
        PlayerMatchState best = null;
        int bestScore = 0, second = 0;
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            if (car == null || !car.HasState) continue;
            if (car.Score > bestScore) { second = bestScore; bestScore = car.Score; best = car; }
            else if (car.Score > second) second = car.Score;
        }
        return best != null && bestScore > second ? best.Object.Id : default;
    }

    // F (the same key PlayerInputSampler sends as UsePowerUp) pressed with an empty slot.
    void CheckNothingToUse(PowerUpType item)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || PauseUI.IsOpen || !keyboard.fKey.wasPressedThisFrame) return;
        if (item != PowerUpType.None || Time.unscaledTime < nextNope) return;
        nextNope = Time.unscaledTime + nopeCooldown;
        Play(nopeClip, nopeVolume, 1f);
    }

    // Your car hits the holder (or the holder is you and gets hit) while the Crown is protected or the
    // holder's Damper is on: a "dead" thud. Each involved player detects it on their own client.
    void CheckBlocked(CrownMatchState crown, NetworkId me)
    {
        if (body == null || !crown.HasHolder || crown.Runner == null) { touchingHolder = false; return; }

        bool iAmHolder = crown.HolderId == me;
        bool hit = false;
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count && !hit; i++)
        {
            var car = cars[i];
            if (car == null || car == local || !car.HasState) continue;
            bool carIsHolder = crown.HolderId == car.Object.Id;
            if (!iAmHolder && !carIsHolder) continue;                 // a block always involves the holder

            var holderCar = iAmHolder ? local : car;
            var holderDamper = holderCar.GetComponent<DamperField>();
            if (!crown.IsProtected && (holderDamper == null || !holderDamper.IsActive)) continue;

            var other = car.GetComponent<BoxCollider>();
            if (other == null || Gap(body, other) > blockContactGap) continue;

            var otherAudio = car.GetComponent<RobotAudioController>();
            Vector3 dir = other.bounds.center - body.bounds.center; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            Vector3 mine = robot != null ? robot.VisibleVelocity : Vector3.zero;
            Vector3 theirs = otherAudio != null ? otherAudio.VisibleVelocity : Vector3.zero;
            float closing = Vector3.Dot(mine - theirs, dir.normalized);
            var handling = HandlingValuesProvider.Current;
            hit = closing >= (handling != null ? handling.minimumApproachSpeed : 1.5f);
        }

        if (hit && !touchingHolder && Time.time >= nextBlock)
        {
            nextBlock = Time.time + blockCooldown;
            PlayVariation(blockedClips, ref lastBlockClip, blockedVolume);
        }
        touchingHolder = hit;
    }

    // Holding the Crown: a rival closing in from behind raises a low swell; fades as they drop back.
    void UpdateRearThreat(bool holding, float dt)
    {
        float intensity = 0f;
        if (holding && view != null)
        {
            Vector3 myPos = view.position;
            Vector3 fwd = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            var cars = PlayerRoster.Active;
            for (int i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                if (car == null || car == local || !car.HasState) continue;
                Vector3 offset = car.transform.position - myPos; offset.y = 0f;
                float d = offset.magnitude;
                if (d > threatRange || d < 0.01f) continue;
                if (Vector3.Dot(offset / d, fwd) > rearDot) continue;    // not behind
                intensity = Mathf.Max(intensity, 1f - d / threatRange);
            }
        }
        FadeThreat(intensity, intensity, dt);
    }

    void FadeThreat(float target, float intensity, float dt)
    {
        if (threatLoopSource == null || threatLoopSource.clip == null) return;
        threatLevel = Mathf.MoveTowards(threatLevel, target, dt / threatFadeSeconds);
        threatLoopSource.volume = threatLevel * threatMaxVolume;
        threatLoopSource.pitch = Mathf.Lerp(threatPitch.x, threatPitch.y, Mathf.Clamp01(intensity));
        if (threatLevel > 0.0001f) { if (!threatLoopSource.isPlaying) threatLoopSource.Play(); }
        else if (threatLoopSource.isPlaying) threatLoopSource.Stop();
    }

    // ---------------- spin-out muffle ----------------

    void UpdateMuffle(bool on, float dt)
    {
        float rate = 1f / (on ? muffleInSeconds : muffleOutSeconds);
        float target = on ? 1f : 0f;
        if (Mathf.Approximately(muffle, target) && !on) return;
        muffle = Mathf.MoveTowards(muffle, target, rate * dt);
        SetMuffle(muffle);
    }

    void SetMuffle(float amount)
    {
        if (amount <= 0f && listenerFilter == null) return;
        if (listenerFilter == null)
        {
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener == null) return;
            listenerFilter = listener.GetComponent<AudioLowPassFilter>();
            if (listenerFilter == null) listenerFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
        }
        // Exponential sweep sounds natural: 22 kHz (open) down to muffleCutoff.
        listenerFilter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(muffleCutoff), amount));
        listenerFilter.enabled = amount > 0f;
    }

    // ---------------- playback ----------------

    void PlayStinger()
    {
        if (stingerSource == null || tookCrownClip == null) return;
        stingerSource.pitch = 1f;
        stingerSource.PlayOneShot(tookCrownClip, tookCrownVolume);
    }

    void PlayItemUsed(PowerUpType item)
    {
        int index = (int)item - 1;
        if (itemUsedClips == null || index < 0 || index >= itemUsedClips.Length) return;
        Play(itemUsedClips[index], itemUsedVolume, 1f);
    }

    void PlayVariation(AudioClip[] clips, ref int last, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length;
        last = i;
        Play(clips[i], volume, 1f + Random.Range(-pitchVariation, pitchVariation));
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || pool == null || pool.Length == 0) return;
        var s = pool[nextPool];
        nextPool = (nextPool + 1) % pool.Length;
        if (s == null) return;
        s.pitch = pitch;
        s.PlayOneShot(clip, volume);
    }

    static void Configure2D(AudioSource s, int priority)
    {
        if (s == null) return;
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        s.dopplerLevel = 0f;
        s.priority = priority;
    }

    static float Gap(BoxCollider a, BoxCollider b)
    {
        Vector3 onB = b.ClosestPoint(a.bounds.center);
        Vector3 onA = a.ClosestPoint(onB);
        onB = b.ClosestPoint(onA);
        return Vector3.Distance(onA, onB);
    }
}
