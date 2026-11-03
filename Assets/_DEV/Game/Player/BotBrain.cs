using Fusion;
using UnityEngine;

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
            RetargetTicksRemaining = Mathf.Max(1, Mathf.RoundToInt(v.botReactionSeconds * tickRate));
        }

        Vector3 forward = FlatForward();
        float steer = SteerToward(TargetPoint, forward, v);

        // Whiskers override the target for this tick only.
        if (TryAvoid(forward, v, out float avoidSteer))
            steer = avoidSteer;

        return new PlayerNetInput
        {
            ThrottleAxis = 1f,  // always held
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
        bool driving = matchState != null && matchState.CanMove && MatchTimerState.AllowsDriving;
        if (!driving)
        {
            StuckTicks = 0;
            return;
        }

        Vector3 velocity = body.linearVelocity;
        float flatSpeed = new Vector2(velocity.x, velocity.z).magnitude;

        StuckTicks = flatSpeed < v.botStuckSpeed ? StuckTicks + 1 : 0;

        int stuckLimit = Mathf.Max(1, Mathf.RoundToInt(v.botStuckSeconds * tickRate));
        bool spunOut = car.IsSpinningOut;

        if (spunOut || StuckTicks >= stuckLimit)
        {
            if (Mode != BotMode.Recover)
            {
                SetMode(BotMode.Recover);
                RetargetTicksRemaining = 0; // pick the centre right away
            }

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
    bool TryAvoid(Vector3 forward, HandlingValues v, out float steer)
    {
        steer = 0f;
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

        if (!lastAvoiding) return false;

        if (leftHit && !rightHit && !forwardHit) steer = 1f;        // wall on the left: go right
        else if (rightHit && !leftHit && !forwardHit) steer = -1f;  // wall on the right: go left
        else if (Mathf.Approximately(leftDist, rightDist)) steer = AimErrorSign(); // dead ahead: fixed side
        else steer = leftDist > rightDist ? -1f : 1f;               // turn toward the freer side

        return true;
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

    float AimErrorSign() => (Object.Id.Raw & 1) == 0 ? 1f : -1f;

    Vector3 FlatForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
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

        if (Mode == BotMode.Run && lastRunDirection != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(origin, lastRunDirection * 2f); // the direction Run picked
        }

        Gizmos.color = lastAvoiding ? Color.red : Color.gray;
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
    }
}