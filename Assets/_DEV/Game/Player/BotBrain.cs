using Fusion;
using UnityEngine;
using UnityEngine.AI;

// The FD-07 bot: a three-state decision layer that produces one PlayerNetInput per tick.
// Host only - BotDriver calls Think from PlayerMovement for cars with no player.
//
//   Chase   (default)          -> the Crown, or whoever holds it
//   Run     (this bot holds)   -> directly away from the nearest rival
//   Recover (spun out / stuck) -> the arena centre
//
// No planning, no prediction, no coordination. Throttle always held, brake never used, never hops,
// drifts or boosts - so bots never land a boosted hit (FD-07's intended skill floor).
//
// The decision state is networked (FD-05 BotState), so after a host migration the new host carries
// on from the same mode and target. On every car, so a car handed to a bot mid-match works too.
[RequireComponent(typeof(NetworkObject))]
public sealed class BotBrain : NetworkBehaviour
{
    public enum BotMode : byte { Chase = 0, Run = 1, Recover = 2 }

    [Header("Debug")]
    [Tooltip("Draws the target line and whiskers in the Scene view (host).")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool logModeChanges = false;

    // FD-05 BotState. Declaration order is the network layout - new fields go at the end.
    [Networked] public BotMode Mode { get; private set; }
    [Networked] public Vector3 TargetPoint { get; private set; }
    [Networked] private int RetargetTicksRemaining { get; set; }
    [Networked] private int StuckTicks { get; set; }
    [Networked] private int RecoverTicksRemaining { get; set; }
    // Avoidance: the side (+1 right, -1 left) chosen when a wall first appeared, kept until the
    // whiskers are clear. Stops the bot flip-flopping when all three see the same wall.
    [Networked] private int AvoidSide { get; set; }
    // Ticks spent nose-close to a wall and nearly stopped.
    [Networked] private int BlockedTicks { get; set; }
    // Back-off manoeuvre: reverse with opposite lock for this many ticks, then drive on.
    [Networked] private int BackOffTicksRemaining { get; set; }
    [Networked] private int BackOffSide { get; set; }
    // Ticks without real progress toward the steer point (scraping, circling, pinned) - option 5.
    [Networked] private int NoProgressTicks { get; set; }

    static HandlingValues Values => HandlingValuesProvider.Current;

    // Shared, host-side lookups. Crates and the Crown are few and rarely change, so one scene search
    // per second serves every bot.
    static PowerUpCrate[] crates = new PowerUpCrate[0];
    static DataCrown dataCrown;
    static OilPatch[] oilPatches = new OilPatch[0];
    static int lastWorldRefreshTick;
    static bool worldEverRefreshed;

    readonly RaycastHit[] hits = new RaycastHit[16];

    Rigidbody body;
    PowerUpInventory inventory;
    PlayerMatchState matchState;
    float chassisLength = 2f;

    // Last tick's whisker result, for gizmos only.
    bool lastAvoiding;
    Vector3 lastLeftDir, lastForwardDir, lastRightDir;
    float lastWhiskerLength;
    Vector3 lastRunDirection;

    public override void Spawned()
    {
        body = GetComponent<Rigidbody>();
        inventory = GetComponent<PowerUpInventory>();
        matchState = GetComponent<PlayerMatchState>();
        chassisLength = MeasureChassisLength();
    }

    // ───────────────────────────── per tick ─────────────────────────────

    public PlayerNetInput Think(PlayerMovement car)
    {
        HandlingValues v = Values;
        int tickRate = Runner.TickRate;

        UpdateRecover(car, v, tickRate);

        // Run and Recover head for a fixed point. Nearly there: pick the next one NOW, instead of
        // arriving at full speed and carrying on into whatever is just past it (a wall, an obstacle).
        if (Mode != BotMode.Chase &&
            FlatDistance(transform.position, TargetPoint) < v.botArriveRetargetDistance + FlatSpeed() * 0.2f)
            RetargetTicksRemaining = 0;

        // FD-07: re-evaluate mode and target only every bot_reaction_ticks.
        RetargetTicksRemaining--;
        if (RetargetTicksRemaining <= 0)
        {
            Retarget(car, v);
            RebuildPath(v); // route to the new target around the walls (NavMesh)
            RetargetTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(v.botReactionSeconds * tickRate));
        }

        // Backing off a wall: reverse with the wheel turned the OTHER way. Steering flips in reverse
        // (SteeringModel), so this swings the nose toward BackOffSide; when it ends, the bot drives
        // off toward that side with the wall beside it rather than in front.
        if (BackOffTicksRemaining > 0)
        {
            BackOffTicksRemaining--;
            StuckTicks = 0; // slow on purpose while backing off - not stuck
            BlockedTicks = 0;
            if (BackOffTicksRemaining == 0) repathNow = true; // fresh route from where we ended up
            return MakeInput(-1f, -BackOffSide);
        }

        // Off the route (pushed, bumped, backed off): route again now rather than at the next re-decide.
        if (repathNow)
        {
            repathNow = false;
            RebuildPath(v);
        }

        Vector3 forward = FlatForward();
        // Follow the NavMesh route; straight at the target when there's no route.
        Vector3 steerPoint = PursuitPoint(v);
        lastPursuitPoint = steerPoint;
        // How fast to go, as a share of top speed: slow for the corner right in front, AND for the
        // next corners along the route before reaching them (brake early, not in the corner).
        float speedShare = Mathf.Min(CornerThrottle(steerPoint, forward, v), UpcomingCornerShare(v));

        float steer;
        bool noseBlocked;
        if (v.botUseContextSteering)
        {
            // One decision from every direction at once: where the route wants to go, minus where
            // the walls, edges, cars and hazards are (option 8).
            steer = ContextSteer(steerPoint, forward, v, out float headingDanger, out noseBlocked);
            speedShare = Mathf.Min(speedShare, Mathf.Lerp(1f, v.botNavCornerThrottle, headingDanger));
        }
        else
        {
            // Older layer: steer at the route, three whiskers override it for this tick only.
            steer = SteerToward(steerPoint, forward, v);
            if (TryAvoid(forward, v, out float avoidSteer, out noseBlocked))
            {
                steer = avoidSteer;
                speedShare = Mathf.Min(speedShare, v.botNavCornerThrottle); // ease off while dodging a wall
            }
        }

        float throttle = SpeedControl(speedShare * v.TopSpeed, v);
        UpdateProgress(car, steerPoint, v, tickRate);

        // Nose against a wall and nearly stopped: steering can't help - the car has no steering
        // authority at a standstill - so back off instead of waiting for the 1.5 s stuck timer.
        if (v.botAllowBackOff && noseBlocked && FlatSpeed() < v.botStuckSpeed && CanDrive())
        {
            BlockedTicks++;
            if (BlockedTicks >= Mathf.Max(1, Mathf.RoundToInt(v.botBlockedSeconds * tickRate)))
                StartBackOff(v, tickRate);
        }
        else
        {
            BlockedTicks = 0;
        }

        return MakeInput(throttle, steer);
    }

    // FD-07 holds the throttle down always. Following a real route, that sends the car wide on every
    // sharp corner and into the wall - it can't turn tight at full speed. So it eases off when the
    // steer point is well off the nose (a corner coming up), never below botNavCornerThrottle.
    // Another deliberate exception to FD-07, like back-off; botNavSlowForCorners = false restores it.
    float CornerThrottle(Vector3 steerPoint, Vector3 forward, HandlingValues v)
    {
        if (!v.botNavSlowForCorners || !hasRoute) return 1f;

        Vector3 to = steerPoint - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return 1f;

        float angle = Mathf.Abs(Vector3.SignedAngle(forward, to, Vector3.up));
        float full = v.botNavSlowAngle;           // full throttle below this
        float slowest = Mathf.Max(full + 1f, 90f); // slowest from here on
        float t = Mathf.InverseLerp(full, slowest, angle);
        return Mathf.Lerp(1f, v.botNavCornerThrottle, t);
    }

    // ─── Context steering (option 8) ──────────────────────────────────────
    //
    // Instead of "follow the route unless a whisker overrides it", every tick the bot scores
    // botContextDirections directions around itself:
    //   interest - how well a direction follows the route (toward the pursuit point), plus a little
    //              for keeping straight on and for the direction it chose last tick (no flicker)
    //   danger   - walls and cliff edges along it (NavMesh), other cars about to be there, oil
    //              patches and charging Repulse Pulses - each 0 (clear) to 1 (right there)
    // The best interest x (1 - danger) wins, blended with its neighbours for a smooth heading. One
    // decision that sees everything at once, so it slides around a rock instead of hitting it,
    // threads between two cars, and never flip-flops between two whiskers.
    // The probe reaches further at speed (botContextLookSeconds ahead), so fast bots react early.
    const int MaxSlots = 32;
    readonly float[] interest = new float[MaxSlots];
    readonly float[] danger = new float[MaxSlots];
    readonly float[] dangerSpread = new float[MaxSlots];
    readonly Vector3[] slotDir = new Vector3[MaxSlots];
    int slotCount;
    int lastChosen = -1;
    Vector3 lastContextDir;
    float lastProbe;
    NetworkId chaseTargetId;

    float ContextSteer(Vector3 steerPoint, Vector3 forward, HandlingValues v, out float chosenDanger, out bool noseBlocked)
    {
        int n = Mathf.Clamp(v.botContextDirections, 8, MaxSlots);
        slotCount = n;
        Vector3 position = transform.position;
        Vector3 origin = position + Vector3.up * 0.5f;

        Vector3 goal = steerPoint - position;
        goal.y = 0f;
        goal = goal.sqrMagnitude > 0.01f ? goal.normalized : forward;

        float probe = Mathf.Max(chassisLength * v.botWhiskerChassisLengths, FlatSpeed() * v.botContextLookSeconds);
        lastProbe = probe;
        PrepareNavWhiskers(v);

        float forwardWall = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(360f * i / n, Vector3.up) * forward; // slot 0 = straight ahead
            slotDir[i] = dir;

            interest[i] = Mathf.Max(0f, Vector3.Dot(dir, goal))
                        + v.botContextForwardBias * Mathf.Max(0f, Vector3.Dot(dir, forward))
                        + (i == lastChosen ? v.botContextStickiness : 0f);

            // Walls and cliff edges: NavMesh rays (physics rays when hugging a wall).
            danger[i] = Whisker(origin, dir, probe, out float wall) ? 1f - wall / probe : 0f;
            if (i == 0 && wall < probe) forwardWall = wall;
        }

        if (v.botContextAvoidCars) AddCarDanger(n, probe, v);
        if (v.botContextAvoidHazards) AddHazardDanger(n, probe, v);

        // Spread a little danger into the neighbouring directions: a margin, so the chosen heading
        // doesn't skim the very edge of a rock.
        for (int i = 0; i < n; i++)
        {
            float left = danger[(i + n - 1) % n], right = danger[(i + 1) % n];
            dangerSpread[i] = Mathf.Max(danger[i], v.botContextDangerSpread * Mathf.Max(left, right));
        }

        int best = -1;
        float bestScore = 0f;
        for (int i = 0; i < n; i++)
        {
            if (dangerSpread[i] >= v.botContextBlockedDanger) continue; // too close: not an option
            float score = interest[i] * (1f - dangerSpread[i]);
            if (score > bestScore) { bestScore = score; best = i; }
        }

        noseBlocked = forwardWall < chassisLength * v.botBlockedDistanceChassisLengths;

        if (best < 0)
        {
            // Nothing useful is open (boxed in, or the only open way is backwards): turn toward the
            // least dangerous side and let the back-off / stuck logic do the rest.
            int safest = 0;
            for (int i = 1; i < n; i++) if (dangerSpread[i] < dangerSpread[safest]) safest = i;
            lastChosen = -1;
            lastContextDir = slotDir[safest];
            chosenDanger = 1f;
            float side = Vector3.SignedAngle(forward, slotDir[safest], Vector3.up);
            return side >= 0f ? 1f : -1f;
        }

        // Smooth heading: lean toward the better neighbour, weighted by its score.
        int prev = (best + n - 1) % n, next = (best + 1) % n;
        float sPrev = interest[prev] * (1f - dangerSpread[prev]);
        float sNext = interest[next] * (1f - dangerSpread[next]);
        Vector3 heading = slotDir[best] * bestScore
                        + slotDir[prev] * Mathf.Max(0f, sPrev) * 0.5f
                        + slotDir[next] * Mathf.Max(0f, sNext) * 0.5f;
        heading.y = 0f;
        heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : slotDir[best];

        lastChosen = best;
        lastContextDir = heading;
        chosenDanger = dangerSpread[best];
        return SteerToward(position + heading * 10f, forward, v);
    }

    // Other cars, where they're about to be (relative motion over the look time). The car being
    // chased is never danger - hitting it is the point. In Run mode every car is danger.
    void AddCarDanger(int n, float probe, HandlingValues v)
    {
        Vector3 position = transform.position;
        Vector3 myVelocity = body.linearVelocity;
        var cars = PlayerRoster.Active;

        for (int c = 0; c < cars.Count; c++)
        {
            var car = cars[c];
            if (car == null || !car.HasState || car.Object == Object) continue;
            if (Mode == BotMode.Chase && car.Object.Id == chaseTargetId) continue;

            Vector3 rel = car.transform.position - position;
            Vector3 relVel = (car.TryGetComponent(out Rigidbody rb) ? rb.linearVelocity : Vector3.zero) - myVelocity;
            rel.y = 0f;
            relVel.y = 0f;

            // Closest approach within the look time.
            float t = relVel.sqrMagnitude > 0.01f
                ? Mathf.Clamp(-Vector3.Dot(rel, relVel) / relVel.sqrMagnitude, 0f, v.botContextLookSeconds)
                : 0f;
            Vector3 future = rel + relVel * t;

            float gap = future.magnitude - chassisLength; // body to body, roughly
            if (gap > probe) continue;

            float weight = Mathf.Clamp01(1f - Mathf.Max(0f, gap) / probe) * v.botContextCarWeight;
            AddDangerCone(n, future, chassisLength, weight);
        }
    }

    // Oil patches and charging Repulse Pulses: circles to steer around.
    void AddHazardDanger(int n, float probe, HandlingValues v)
    {
        Vector3 position = transform.position;

        for (int i = 0; i < oilPatches.Length; i++)
        {
            var patch = oilPatches[i];
            if (patch == null || !patch.HasState || !patch.IsActive) continue;
            AddCircleDanger(n, patch.Center - position, patch.Radius, probe, v.botContextHazardWeight);
        }

        var cars = PlayerRoster.Active;
        for (int c = 0; c < cars.Count; c++)
        {
            var car = cars[c];
            if (car == null || !car.HasState || car.Object == Object) continue;
            if (!car.TryGetComponent(out RepulsePulse pulse) || !pulse.IsCharging) continue;
            AddCircleDanger(n, car.transform.position - position, pulse.Radius, probe, v.botContextHazardWeight);
        }
    }

    void AddCircleDanger(int n, Vector3 toCentre, float radius, float probe, float maxWeight)
    {
        toCentre.y = 0f;
        float gap = toCentre.magnitude - radius;
        if (gap > probe) return;
        float weight = Mathf.Clamp01(1f - Mathf.Max(0f, gap) / probe) * maxWeight;
        AddDangerCone(n, toCentre, radius, weight);
    }

    // Marks every direction that points into a circle of 'size' at 'offset' as dangerous, strongest
    // straight at its centre.
    void AddDangerCone(int n, Vector3 offset, float size, float weight)
    {
        float distance = offset.magnitude;
        if (distance < 0.01f) { for (int i = 0; i < n; i++) danger[i] = Mathf.Max(danger[i], weight); return; }

        Vector3 dir = offset / distance;
        float halfAngle = Mathf.Atan2(size, distance) * Mathf.Rad2Deg + 360f / n * 0.5f;
        float cosLimit = Mathf.Cos(Mathf.Min(89f, halfAngle) * Mathf.Deg2Rad);

        for (int i = 0; i < n; i++)
        {
            float align = Vector3.Dot(slotDir[i], dir);
            if (align < cosLimit) continue;
            float k = Mathf.InverseLerp(cosLimit, 1f, align); // 0 at the cone's edge, 1 dead centre
            danger[i] = Mathf.Max(danger[i], weight * Mathf.Lerp(0.5f, 1f, k));
        }
    }

    // ─── Speed (option 4) ─────────────────────────────────────────────────
    //
    // The drive model's throttle is on/off: any positive input drives toward full top speed. So a
    // "40% throttle" was really full throttle - the old corner slowing never slowed anything. To go
    // slower, the bot now COASTS (no throttle) or BRAKES (-1) until it's down to the speed it wants.
    float SpeedControl(float desiredSpeed, HandlingValues v)
    {
        float speed = Vector3.Dot(body.linearVelocity, transform.forward);
        if (speed > desiredSpeed + v.botBrakeMargin && speed > 1f) return -1f; // well too fast: brake
        if (speed > desiredSpeed) return 0f;                                     // a bit too fast: coast
        return 1f;
    }

    // Looks along the route for the coming corners and returns the share of top speed it can carry
    // NOW and still be slow enough when it gets there (braking at botCornerDecel). Sharper corner =
    // lower corner speed; nearer corner = less room to brake.
    float UpcomingCornerShare(HandlingValues v)
    {
        if (!v.botNavSlowForCorners || !hasRoute || v.TopSpeed <= 0f) return 1f;

        Vector3 position = transform.position;
        float distance = FlatDistance(position, corners[cornerIndex]); // to the end of the current segment
        float allowed = v.TopSpeed;

        for (int i = cornerIndex; i < cornerCount - 1 && distance <= v.botCornerLookAhead; i++)
        {
            Vector3 inDir = corners[i] - corners[i - 1];
            Vector3 outDir = corners[i + 1] - corners[i];
            inDir.y = 0f;
            outDir.y = 0f;

            if (inDir.sqrMagnitude > 0.01f && outDir.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.Angle(inDir, outDir); // 0 = straight on, 90 = right-angle turn
                float cornerShare = angle <= 90f
                    ? Mathf.Lerp(1f, v.botCornerSpeedAt90, angle / 90f)
                    : Mathf.Lerp(v.botCornerSpeedAt90, v.botNavCornerThrottle, (angle - 90f) / 90f);
                float cornerSpeed = cornerShare * v.TopSpeed;

                // Fastest speed from which it can still brake down to cornerSpeed in 'distance'.
                float canCarry = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * v.botCornerDecel * distance);
                allowed = Mathf.Min(allowed, canCarry);
            }

            distance += FlatDistance(corners[i], corners[i + 1]);
        }

        // Fixed goals (Run, Recover): ease off on the final approach, as a safety net in case the
        // early re-pick above can't find a new point in time. Chase never slows for its target.
        if (Mode != BotMode.Chase)
        {
            float toEnd = FlatDistance(position, corners[cornerCount - 1]);
            float endSpeed = v.botArriveSpeedShare * v.TopSpeed;
            allowed = Mathf.Min(allowed, Mathf.Sqrt(endSpeed * endSpeed + 2f * v.botCornerDecel * toEnd));
        }

        return Mathf.Clamp01(allowed / v.TopSpeed);
    }

    // ─── Progress (option 5) ──────────────────────────────────────────────
    //
    // The speed-based stuck timer misses a car that IS moving but getting nowhere: scraping along a
    // wall, circling a rock, pushed back by another car. This measures closing speed toward the point
    // it's steering at; too little for botNoProgressSeconds = stuck: back off and route again.
    void UpdateProgress(PlayerMovement car, Vector3 steerPoint, HandlingValues v, int tickRate)
    {
        if (!CanDrive() || car.IsSpinningOut)
        {
            NoProgressTicks = 0;
            return;
        }

        Vector3 to = steerPoint - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 1f) { NoProgressTicks = 0; return; } // already there

        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        float closing = Vector3.Dot(velocity, to.normalized);

        if (closing < v.botMinProgressSpeed) NoProgressTicks++;
        else NoProgressTicks = Mathf.Max(0, NoProgressTicks - 2); // real progress clears it quickly

        if (NoProgressTicks < Mathf.Max(1, Mathf.RoundToInt(v.botNoProgressSeconds * tickRate))) return;

        NoProgressTicks = 0;
        repathNow = true;
        if (v.botAllowBackOff && BackOffTicksRemaining <= 0) StartBackOff(v, tickRate);
        if (logModeChanges) Debug.Log($"[Bot] {name}: no progress for {v.botNoProgressSeconds:F1}s - backing off and re-routing");
    }

    PlayerNetInput MakeInput(float throttle, float steer)
    {
        return new PlayerNetInput
        {
            ThrottleAxis = throttle,
            SteerAxis = steer,
            Boost = false,      // never
            Drift = false,
            Hop = false,        // never: no drift, no window, no chain
            // Fire whatever it rolled, immediately (FD-07). PlayerMovement fires on a press edge,
            // so the button alternates each tick while the slot is full: it fires within 2 ticks.
            UsePowerUp = HasItem() && (Runner.Tick.Raw & 1) == 0,
        };
    }

    // ───────────────────────────── Recover ──────────────────────────────

    void UpdateRecover(PlayerMovement car, HandlingValues v, int tickRate)
    {
        // Parked for the countdown (or after the buzzer) isn't stuck.
        if (!CanDrive())
        {
            StuckTicks = 0;
            BlockedTicks = 0;
            BackOffTicksRemaining = 0;
            return;
        }

        StuckTicks = FlatSpeed() < v.botStuckSpeed ? StuckTicks + 1 : 0;

        int stuckLimit = Mathf.Max(1, Mathf.RoundToInt(v.botStuckSeconds * tickRate));
        bool spunOut = car.IsSpinningOut;

        if (spunOut || StuckTicks >= stuckLimit)
        {
            if (Mode != BotMode.Recover)
            {
                SetMode(BotMode.Recover);
                RetargetTicksRemaining = 0; // pick the centre right away
            }

            // Stuck (not spun out): whatever pinned it - a wall, a corner, another car - driving
            // forward into it again won't help. Back off first, then head for the centre.
            if (!spunOut && v.botAllowBackOff && BackOffTicksRemaining <= 0)
                StartBackOff(v, tickRate);

            StuckTicks = 0;
            RecoverTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(v.botRecoverSeconds * tickRate));
            return;
        }

        if (Mode == BotMode.Recover)
        {
            RecoverTicksRemaining--;
            if (RecoverTicksRemaining <= 0)
            {
                SetMode(BotMode.Chase); // Retarget decides Chase or Run
                RetargetTicksRemaining = 0;
            }
        }
    }

    // ───────────────────────────── decisions ────────────────────────────

    void Retarget(PlayerMovement car, HandlingValues v)
    {
        RefreshWorld();

        if (Mode == BotMode.Recover)
        {
            TargetPoint = ArenaCenter(v);
            return;
        }

        CrownMatchState crown = CrownMatchState.Instance;
        bool holdingCrown = crown != null && crown.IsHolder(Object);

        if (holdingCrown)
        {
            SetMode(BotMode.Run);
            TargetPoint = RunTarget(v);
        }
        else
        {
            SetMode(BotMode.Chase);
            TargetPoint = ChaseTarget(crown, v);
        }

        // Crates are driven over, never detoured to: only one already within bot_crate_angle of the
        // heading, and nearer than the target, replaces the target.
        if (!HasItem() && TryCrateAhead(v, out Vector3 cratePoint))
            TargetPoint = cratePoint;
    }

    Vector3 ChaseTarget(CrownMatchState crown, HandlingValues v)
    {
        // Held: drive at the holder.
        chaseTargetId = default;
        if (crown != null && crown.HasHolder &&
            Runner.TryFindObject(crown.HolderId, out NetworkObject holder))
        {
            chaseTargetId = holder.Id; // context steering never treats the car we're chasing as danger
            return holder.transform.position;
        }

        // Unheld (telegraphing or free): drive at the Crown - the opening race.
        if (dataCrown != null && dataCrown.Object != null && dataCrown.Object.IsValid)
            return dataCrown.transform.position;

        // No Crown yet (pre-spawn): wait near the middle.
        return ArenaCenter(v);
    }

    // Directions tried for Run, as offsets from "directly away", nearest first. Fixed order, so the
    // choice is deterministic.
    static readonly float[] runAngleOffsets = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };

    // FD-07 says "directly away from the nearest rival". Taken literally that can point at a wall a
    // metre away, so the bot drives into it, stops and drops into Recover. Instead it tries directions
    // close to "away", nearest first, and takes the first one that:
    //   - has at least botRunMinDistance of open floor before a wall (checked with a sweep the width
    //     of the car), and
    //   - ends further from the nearest rival than the bot is now, so it's still running away.
    // The target is pulled in short of the wall by botRunWallMargin, so it's always inside the arena.
    // Cornered with no such direction: cut back through the open centre (FD-06), like Recover.
    Vector3 RunTarget(HandlingValues v)
    {
        Vector3 position = transform.position;
        PlayerMatchState nearest = NearestRival(position);

        if (nearest == null)
            return ArenaCenter(v);

        Vector3 rival = nearest.transform.position;
        Vector3 away = position - rival;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = FlatForward();
        away.Normalize();

        float currentGapSqr = FlatSqr(position - rival);
        float minDistance = Mathf.Min(v.botRunMinDistance, v.botRunDistance);

        for (int i = 0; i < runAngleOffsets.Length; i++)
        {
            Vector3 direction = Quaternion.AngleAxis(runAngleOffsets[i], Vector3.up) * away;

            float open = OpenDistance(position, direction, v.botRunDistance + v.botRunWallMargin);
            float usable = Mathf.Min(v.botRunDistance, open - v.botRunWallMargin);
            if (usable < minDistance) continue;

            // The escape point must be clear of walls, obstacles and edges on EVERY side - a point 3 m
            // in front of a wall can still sit right beside a rock. Shorten it step by step if that
            // helps; otherwise try the next direction.
            for (float d = usable; d >= minDistance; d -= 2f)
            {
                Vector3 target = position + direction * d;
                if (FlatSqr(target - rival) <= currentGapSqr) break; // would run toward the rival
                if (!HasClearance(target, v.botRunWallMargin, v)) continue;

                lastRunDirection = direction;
                return target;
            }
        }

        lastRunDirection = Vector3.zero;
        return ArenaCenter(v);
    }

    // Is there at least 'clearance' metres of open space all around this point?
    //   NavMesh: distance to the nearest edge (walls, baked obstacles, cliff edges) + agent radius
    //   Physics: 8 short rays for colliders the NavMesh doesn't know about
    bool HasClearance(Vector3 point, float clearance, HandlingValues v)
    {
        if (v.botUseNavMesh && TryGetAgentType(v, point))
        {
            var filter = new NavMeshQueryFilter { agentTypeID = navAgentTypeId, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(point, out NavMeshHit onMesh, v.botNavSnapDistance, filter)) return false;
            if (NavMesh.FindClosestEdge(onMesh.position, out NavMeshHit edge, filter))
            {
                float radius = NavMesh.GetSettingsByID(navAgentTypeId).agentRadius;
                if (edge.distance + radius < clearance) return false;
            }
        }

        Vector3 origin = point + Vector3.up * 0.5f;
        for (int k = 0; k < 8; k++)
        {
            Vector3 dir = Quaternion.AngleAxis(45f * k, Vector3.up) * Vector3.forward;
            if (PhysicsWhisker(origin, dir, clearance, out _)) return false;
        }
        return true;
    }

    PlayerMatchState NearestRival(Vector3 position)
    {
        PlayerMatchState nearest = null;
        float nearestSqr = float.MaxValue;

        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
        {
            PlayerMatchState car = cars[i];
            if (car == null || !car.HasState || car.Object == Object) continue;

            float sqr = (car.transform.position - position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = car;
            }
        }
        return nearest;
    }

    // Open floor along a direction before the first wall, swept with a sphere about the car's width
    // so gaps the car can't fit through count as blocked. Same filter as the whiskers: cars and
    // drivable slopes don't count as walls.
    float OpenDistance(Vector3 position, Vector3 direction, float maxDistance)
    {
        // With a NavMesh: measure along the drivable surface instead. It stops at walls AND at cliff
        // edges, and stays on the level the bot is on - a physics sweep at car height sails straight
        // over a plateau's edge and picks a "free" point in mid-air, which the bot then drives at.
        // Then the physics sweep too, nearest wins: it catches what the NavMesh doesn't know about
        // (invisible boundary walls outside the bake, NavMeshObstacles that don't carve), which Run
        // used to pick escape points right behind.
        float open = maxDistance;
        if (TryNavOpenDistance(position, direction, maxDistance, out float navOpen))
            open = navOpen;
        return Mathf.Min(open, PhysicsOpenDistance(position, direction, open));
    }

    float PhysicsOpenDistance(Vector3 position, Vector3 direction, float maxDistance)
    {

        // Start the sweep above the floor: a sphere that begins overlapping the ground would report
        // a hit at distance 0 with a sideways normal, and every direction would look blocked.
        float radius = Mathf.Max(0.3f, chassisLength * 0.3f);
        Vector3 origin = position + Vector3.up * (radius + 0.3f);
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, maxDistance, ~0, QueryTriggerInteraction.Ignore);

        float open = maxDistance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider.attachedRigidbody != null) continue;
            if (hit.normal.y > 0.5f) continue;
            // Overlapping at the start (Unity reports point zero): ramps under a tilted car, mostly.
            // Skip it - the whiskers and Recover deal with a car already pressed against a wall.
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
            if (hit.distance < open) open = hit.distance;
        }
        return open;
    }

    static float FlatSqr(Vector3 v) => v.x * v.x + v.z * v.z;

    bool TryCrateAhead(HandlingValues v, out Vector3 point)
    {
        point = default;
        Vector3 position = transform.position;
        Vector3 forward = FlatForward();
        float targetSqr = (TargetPoint - position).sqrMagnitude;
        float bestSqr = float.MaxValue;
        bool found = false;

        for (int i = 0; i < crates.Length; i++)
        {
            PowerUpCrate crate = crates[i];
            if (crate == null || crate.Object == null || !crate.Object.IsValid || !crate.IsAvailable) continue;

            Vector3 offset = crate.transform.position - position;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr >= targetSqr || sqr >= bestSqr) continue;
            if (Vector3.Angle(forward, offset) > v.botCrateAngle) continue;

            bestSqr = sqr;
            point = crate.transform.position;
            found = true;
        }

        return found;
    }

    // ───────────────────────────── driving ──────────────────────────────

    float SteerToward(Vector3 target, Vector3 forward, HandlingValues v)
    {
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f) return 0f;

        // Positive = target to the right. bot_aim_error is a fixed constant per bot, not a per-tick
        // draw; its side comes from the car's NetworkId, so it's stable and deterministic.
        float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up) + AimErrorSign() * v.botAimErrorDegrees;
        return Mathf.Clamp(angle / Mathf.Max(1f, v.botSteerFullAngle), -1f, 1f);
    }

    // Three short whiskers. Only static walls count: cars are what a bot is trying to hit, and
    // slopes (normal pointing up) are drivable ramps, not obstacles.
    //
    // The turn side is chosen once, when a wall first appears, and kept until all three whiskers are
    // clear. Re-choosing every tick made a bot facing a wall head-on flip left/right as the
    // distances wobbled, so it never committed to either and sat there.
    bool TryAvoid(Vector3 forward, HandlingValues v, out float steer, out bool noseBlocked)
    {
        steer = 0f;
        noseBlocked = false;
        // On a NavMesh route the path already keeps clear of walls, so the whiskers are shorter: long
        // ones fire constantly in narrow lanes and fight the route, steering the car off it.
        float length = chassisLength * v.botWhiskerChassisLengths * (hasRoute ? v.botNavWhiskerScale : 1f);
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        PrepareNavWhiskers(v);

        Vector3 leftDir = Quaternion.AngleAxis(-v.botWhiskerSideAngle, Vector3.up) * forward;
        Vector3 rightDir = Quaternion.AngleAxis(v.botWhiskerSideAngle, Vector3.up) * forward;

        bool leftHit = Whisker(origin, leftDir, length, out float leftDist);
        bool forwardHit = Whisker(origin, forward, length, out float forwardDist);
        bool rightHit = Whisker(origin, rightDir, length, out float rightDist);

        lastLeftDir = leftDir;
        lastForwardDir = forward;
        lastRightDir = rightDir;
        lastWhiskerLength = length;
        lastAvoiding = leftHit || forwardHit || rightHit;

        if (!lastAvoiding)
        {
            AvoidSide = 0; // clear: release the latch
            return false;
        }

        noseBlocked = forwardHit && forwardDist < chassisLength * v.botBlockedDistanceChassisLengths;

        if (AvoidSide == 0)
        {
            if (leftHit && !rightHit && !forwardHit) AvoidSide = 1;        // wall on the left: go right
            else if (rightHit && !leftHit && !forwardHit) AvoidSide = -1;  // wall on the right: go left
            else if (Mathf.Abs(leftDist - rightDist) < 0.25f)               // square-on: prefer the target's side
                AvoidSide = TargetSide(forward);
            else AvoidSide = leftDist > rightDist ? -1 : 1;                 // turn toward the freer side
        }

        steer = AvoidSide;
        return true;
    }

    // Which side the target is on (+1 right, -1 left); the fixed per-bot side if it's dead ahead or behind.
    int TargetSide(Vector3 forward)
    {
        Vector3 toTarget = TargetPoint - transform.position;
        toTarget.y = 0f;
        float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
        if (Mathf.Abs(angle) < 5f || Mathf.Abs(angle) > 175f) return (int)AimErrorSign();
        return angle > 0f ? 1 : -1;
    }

    void StartBackOff(HandlingValues v, int tickRate)
    {
        // Turn toward the side avoidance already picked; otherwise toward the target.
        BackOffSide = AvoidSide != 0 ? AvoidSide : TargetSide(FlatForward());
        AvoidSide = BackOffSide; // and keep turning that way once moving forward again
        BackOffTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(v.botBackOffSeconds * tickRate));
        BlockedTicks = 0;
        StuckTicks = 0;
        if (logModeChanges) Debug.Log($"[Bot] {name}: backing off, turning {(BackOffSide > 0 ? "right" : "left")}");
    }

    // ─── NavMesh whiskers (option 3) ─────────────────────────────────────
    //
    // Whiskers measure to the NavMesh edge instead of raycasting colliders: they agree exactly with
    // the route, ignore decorations, and catch cliff edges (the plateau) that physics rays sail over.
    // The edge sits one agent radius in from the wall, so that radius is added back to give the
    // same "distance to the wall" the physics whiskers report. A car pressed so close to a wall that
    // it's off the mesh falls back to physics rays for that tick (they're exact up close).
    bool navWhiskersReady;
    Vector3 navWhiskerStart;
    float navAgentRadius;

    void PrepareNavWhiskers(HandlingValues v)
    {
        navWhiskersReady = false;
        if (!v.botUseNavMesh || !v.botNavWhiskers || !TryGetAgentType(v, transform.position)) return;

        var filter = new NavMeshQueryFilter { agentTypeID = navAgentTypeId, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, filter)) return;

        Vector3 offset = hit.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 0.3f * 0.3f) return; // off the mesh (hugging a wall): use physics

        navWhiskerStart = hit.position;
        navAgentRadius = NavMesh.GetSettingsByID(navAgentTypeId).agentRadius;
        navWhiskersReady = true;
    }

    // Both checks, nearest wins:
    //   NavMesh ray - cliff edges and anything baked into the NavMesh
    //   physics ray - every static collider, baked or not: invisible boundary walls outside the
    //                 bake, objects with a NavMeshObstacle (not carving), anything added after the bake
    // The NavMesh alone missed those, so a bot could plan straight into them.
    bool Whisker(Vector3 origin, Vector3 direction, float length, out float distance)
    {
        distance = length;
        bool found = false;

        if (navWhiskersReady)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = navAgentTypeId, areaMask = NavMesh.AllAreas };
            if (NavMesh.Raycast(navWhiskerStart, navWhiskerStart + direction * length, out NavMeshHit edge, filter))
            {
                float toWall = edge.distance + navAgentRadius;
                if (toWall < distance)
                {
                    distance = toWall;
                    found = true;
                }
            }
        }

        if (PhysicsWhisker(origin, direction, distance, out float physicsWall))
        {
            distance = physicsWall;
            found = true;
        }
        return found;
    }

    // Nearest static, wall-like collider along a ray (cars and floors/ramps ignored).
    bool PhysicsWhisker(Vector3 origin, Vector3 direction, float length, out float distance)
    {
        distance = length;
        int count = Physics.RaycastNonAlloc(origin, direction, hits, length, ~0, QueryTriggerInteraction.Ignore);

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider.attachedRigidbody != null) continue; // cars, including this one
            if (hit.normal.y > 0.5f) continue;                    // ramps and floor
            if (hit.distance < distance)
            {
                distance = hit.distance;
                found = true;
            }
        }
        return found;
    }

    // ───────────────────────────── helpers ──────────────────────────────

    bool HasItem() => inventory != null && inventory.HasItem;

    bool CanDrive() => matchState != null && matchState.CanMove && MatchTimerState.AllowsDriving;

    float FlatSpeed()
    {
        Vector3 velocity = body.linearVelocity;
        return new Vector2(velocity.x, velocity.z).magnitude;
    }

    float AimErrorSign() => (Object.Id.Raw & 1) == 0 ? 1f : -1f;

    Vector3 FlatForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    // ───────────────────────────── NavMesh routing ──────────────────────
    //
    // The NavMesh only answers "which way?" - the bot still drives with throttle and steer through
    // the same pipeline as players (FD-07). A route is calculated when the bot re-decides (every
    // botReactionSeconds), not every tick. Host-only and local: nothing here is networked - after a
    // host migration the next re-decide simply builds a fresh route.
    //
    // No NavMesh baked, no route found, or the bot is off the mesh (mid-jump): the bot steers
    // straight at its target as before, and the whiskers keep it off the walls.

    NavMeshPath navPath;
    readonly Vector3[] corners = new Vector3[32];
    int cornerCount;
    int cornerIndex;   // end of the route segment being followed (corners[cornerIndex - 1] -> corners[cornerIndex])
    bool hasRoute;
    bool repathNow;
    Vector3 lastPursuitPoint; // gizmo only

    // The NavMesh agent type the arena was baked for (the car-sized one), found once and shared.
    static int navAgentTypeId = -1;
    static bool navAgentWarned;

    void RebuildPath(HandlingValues v)
    {
        hasRoute = false;
        if (!v.botUseNavMesh || !TryGetAgentType(v, transform.position)) return;

        var filter = new NavMeshQueryFilter { agentTypeID = navAgentTypeId, areaMask = NavMesh.AllAreas };

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, v.botNavSnapDistance, filter)) return;
        if (!TrySampleGoal(TargetPoint, v, filter, out Vector3 goal)) return;

        navPath ??= new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, goal, filter, navPath)) return;
        if (navPath.status == NavMeshPathStatus.PathInvalid) return;
        // PathPartial is fine: it gets as close as the mesh allows, then the bot steers direct.

        cornerCount = navPath.GetCornersNonAlloc(corners);
        if (cornerCount < 2) return;

        cornerIndex = 1; // corner 0 is where we already are
        hasRoute = true;
    }

    // Open distance across the NavMesh from the bot's position, along a direction: up to the first
    // mesh edge (a wall, a cliff, a pool). False when there's no NavMesh to ask.
    bool TryNavOpenDistance(Vector3 position, Vector3 direction, float maxDistance, out float open)
    {
        open = maxDistance;
        HandlingValues v = Values;
        if (!v.botUseNavMesh || !TryGetAgentType(v, position)) return false;

        var filter = new NavMeshQueryFilter { agentTypeID = navAgentTypeId, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(position, out NavMeshHit start, v.botNavSnapDistance, filter)) return false;

        Vector3 end = start.position + direction * maxDistance;
        if (NavMesh.Raycast(start.position, end, out NavMeshHit edge, filter))
            open = edge.distance;
        return true;
    }

    // Targets aren't always on the floor: the Crown floats, the arena centre has no height, a car
    // can be mid-jump. Snap near the point first, then at our own height, a little wider.
    bool TrySampleGoal(Vector3 point, HandlingValues v, NavMeshQueryFilter filter, out Vector3 goal)
    {
        goal = point;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, v.botNavSnapDistance, filter))
        {
            goal = hit.position;
            return true;
        }

        Vector3 level = new Vector3(point.x, transform.position.y, point.z);
        if (NavMesh.SamplePosition(level, out hit, v.botNavSnapDistance * 3f, filter))
        {
            goal = hit.position;
            return true;
        }
        return false;
    }

    // Where to steer this tick: a point botNavLookAhead metres further along the route from the
    // closest point on it ("pure pursuit"). The car follows the route's line smoothly, starts turning
    // before corners and doesn't cut across them into walls. Drifted too far off the route - pushed
    // by another car, after a back-off - it asks for a fresh route.
    Vector3 PursuitPoint(HandlingValues v)
    {
        if (!hasRoute) return TargetPoint;

        Vector3 position = transform.position;

        // Project onto the current segment (corners[i-1] -> corners[i]); move on once past its end.
        Vector3 closest = ClosestOnSegment(position, corners[cornerIndex - 1], corners[cornerIndex], out float along);
        while (along >= 1f && cornerIndex < cornerCount - 1)
        {
            cornerIndex++;
            closest = ClosestOnSegment(position, corners[cornerIndex - 1], corners[cornerIndex], out along);
        }

        if (FlatDistance(position, closest) > v.botNavOffRouteDistance) repathNow = true;

        // Walk the look-ahead distance along the route from the closest point.
        float remaining = Mathf.Max(0.5f, v.botNavLookAhead);
        Vector3 from = closest;
        for (int i = cornerIndex; i < cornerCount; i++)
        {
            float segment = FlatDistance(from, corners[i]);
            if (segment >= remaining)
                return Vector3.Lerp(from, corners[i], remaining / Mathf.Max(0.0001f, segment));
            remaining -= segment;
            from = corners[i];
        }

        // Past the end: the route's end, the target snapped onto the floor (not the raw target, which
        // can be in the air beyond a ledge or on another level, through a wall).
        return corners[cornerCount - 1];
    }

    static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
    {
        Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
        Vector2 ap = new Vector2(p.x - a.x, p.z - a.z);
        float lengthSqr = ab.sqrMagnitude;
        t = lengthSqr > 0.0001f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / lengthSqr) : 1f;
        return Vector3.Lerp(a, b, t);
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // Uses the agent type named in botNavAgentType; if that's empty, the first agent type that has a
    // NavMesh under the bot - so a car-sized agent type works without any setup.
    static bool TryGetAgentType(HandlingValues v, Vector3 probe)
    {
        if (navAgentTypeId >= 0) return true;

        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
            if (!string.IsNullOrEmpty(v.botNavAgentType) && NavMesh.GetSettingsNameFromID(id) == v.botNavAgentType)
            {
                navAgentTypeId = id;
                return true;
            }
        }

        if (string.IsNullOrEmpty(v.botNavAgentType))
        {
            for (int i = 0; i < count; i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                var filter = new NavMeshQueryFilter { agentTypeID = id, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(probe, out _, 5f, filter))
                {
                    navAgentTypeId = id;
                    Debug.Log($"[Bot] Using NavMesh agent type '{NavMesh.GetSettingsNameFromID(id)}'.");
                    return true;
                }
            }
        }

        if (!navAgentWarned)
        {
            navAgentWarned = true;
            Debug.LogWarning("[Bot] No NavMesh found for the bots - they steer straight at targets. " +
                             "Bake a NavMeshSurface on the arena, or check botNavAgentType.");
        }
        return false;
    }

    static Vector3 ArenaCenter(HandlingValues v) => new Vector3(v.botArenaCenterX, 0f, v.botArenaCenterZ);

    void SetMode(BotMode mode)
    {
        if (Mode == mode) return;
        if (logModeChanges) Debug.Log($"[Bot] {name}: {Mode} -> {mode}");
        Mode = mode;
    }

    void RefreshWorld()
    {
        int tick = Runner.Tick.Raw;
        bool crownMissing = dataCrown == null;
        if (worldEverRefreshed && !crownMissing && tick - lastWorldRefreshTick < Runner.TickRate) return;

        worldEverRefreshed = true;
        lastWorldRefreshTick = tick;
        crates = FindObjectsByType<PowerUpCrate>(FindObjectsSortMode.None);
        dataCrown = FindFirstObjectByType<DataCrown>();
        oilPatches = FindObjectsByType<OilPatch>(FindObjectsSortMode.None);
    }

    float MeasureChassisLength()
    {
        float longest = 0f;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            Vector3 size = c.bounds.size;
            longest = Mathf.Max(longest, Mathf.Max(size.x, size.z));
        }
        return longest > 0.1f ? longest : 2f;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || Object == null || !Object.IsValid || !Object.HasStateAuthority) return;

        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // Context steering: each direction red by danger (short = blocked), the chosen heading green.
        if (Values.botUseContextSteering && slotCount > 0)
        {
            for (int i = 0; i < slotCount; i++)
            {
                float d = dangerSpread[i];
                Gizmos.color = Color.Lerp(new Color(1f, 1f, 1f, 0.25f), Color.red, d);
                Gizmos.DrawRay(origin, slotDir[i] * lastProbe * (1f - d * 0.7f));
            }
            Gizmos.color = Color.green;
            Gizmos.DrawRay(origin + Vector3.up * 0.1f, lastContextDir * lastProbe);
        }

        Gizmos.color = Mode == BotMode.Run ? Color.yellow : Mode == BotMode.Recover ? Color.magenta : Color.cyan;
        Gizmos.DrawLine(origin, TargetPoint);
        Gizmos.DrawWireSphere(TargetPoint, 0.5f);

        // The NavMesh route: white, from the current corner on; the point being steered at: green.
        if (hasRoute)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastPursuitPoint + Vector3.up * 0.3f, 0.35f);
            Gizmos.color = Color.white;
            Vector3 from = transform.position;
            for (int i = cornerIndex; i < cornerCount; i++)
            {
                Gizmos.DrawLine(from + Vector3.up * 0.3f, corners[i] + Vector3.up * 0.3f);
                from = corners[i];
            }
        }

        if (Mode == BotMode.Run && lastRunDirection != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(origin, lastRunDirection * 2f); // the direction Run picked
        }

        Gizmos.color = BackOffTicksRemaining > 0 ? new Color(1f, 0.5f, 0f) : lastAvoiding ? Color.red : Color.gray;
        Gizmos.DrawRay(origin, lastLeftDir * lastWhiskerLength);
        Gizmos.DrawRay(origin, lastForwardDir * lastWhiskerLength);
        Gizmos.DrawRay(origin, lastRightDir * lastWhiskerLength);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        crates = new PowerUpCrate[0];
        dataCrown = null;
        oilPatches = new OilPatch[0];
        lastWorldRefreshTick = 0;
        worldEverRefreshed = false;
        navAgentTypeId = -1;
        navAgentWarned = false;
    }
}