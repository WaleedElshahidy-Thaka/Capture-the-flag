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

    static HandlingValues Values => HandlingValuesProvider.Current;

    // Shared, host-side lookups. Crates and the Crown are few and rarely change, so one scene search
    // per second serves every bot.
    static PowerUpCrate[] crates = new PowerUpCrate[0];
    static DataCrown dataCrown;
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
            return MakeInput(-1f, -BackOffSide);
        }

        Vector3 forward = FlatForward();
        // Steer at the next corner of the NavMesh route; straight at the target when there's no route.
        float steer = SteerToward(NavSteerPoint(v), forward, v);

        // Whiskers override the target for this tick only.
        if (TryAvoid(forward, v, out float avoidSteer, out bool noseBlocked))
            steer = avoidSteer;

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

        return MakeInput(1f, steer); // throttle always held while driving forward
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
        if (crown != null && crown.HasHolder &&
            Runner.TryFindObject(crown.HolderId, out NetworkObject holder))
            return holder.transform.position;

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

            Vector3 target = position + direction * usable;
            if (FlatSqr(target - rival) <= currentGapSqr) continue; // would run toward the rival

            lastRunDirection = direction;
            return target;
        }

        lastRunDirection = Vector3.zero;
        return ArenaCenter(v);
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
        float length = chassisLength * v.botWhiskerChassisLengths;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

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

    bool Whisker(Vector3 origin, Vector3 direction, float length, out float distance)
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
    int cornerIndex;
    bool hasRoute;

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

    // Where to steer this tick: the next corner, blending toward the one after it as we get close,
    // so the car starts turning early instead of overshooting (a car can't stop and pivot).
    Vector3 NavSteerPoint(HandlingValues v)
    {
        if (!hasRoute) return TargetPoint;

        Vector3 position = transform.position;
        while (cornerIndex < cornerCount - 1 &&
               FlatDistance(position, corners[cornerIndex]) < v.botNavCornerReachDistance)
            cornerIndex++;

        // Heading for the last corner = the target itself: aim at the live target point.
        if (cornerIndex >= cornerCount - 1) return TargetPoint;

        Vector3 corner = corners[cornerIndex];
        Vector3 next = corners[cornerIndex + 1];
        float distance = FlatDistance(position, corner);

        if (v.botNavLookAhead > 0f && distance < v.botNavLookAhead)
        {
            float t = 1f - distance / v.botNavLookAhead;
            return Vector3.Lerp(corner, next, t * 0.5f);
        }
        return corner;
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

        Gizmos.color = Mode == BotMode.Run ? Color.yellow : Mode == BotMode.Recover ? Color.magenta : Color.cyan;
        Gizmos.DrawLine(origin, TargetPoint);
        Gizmos.DrawWireSphere(TargetPoint, 0.5f);

        // The NavMesh route: white, from the current corner on.
        if (hasRoute)
        {
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
        lastWorldRefreshTick = 0;
        worldEverRefreshed = false;
        navAgentTypeId = -1;
        navAgentWarned = false;
    }
}