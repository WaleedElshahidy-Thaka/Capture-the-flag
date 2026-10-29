using UnityEngine;

// Every tuning value the drive model reads (GDD doc 03 tuning table). A plain serializable
// class so it can later be wrapped in a ScriptableObject handling profile without touching callers.
// Field names are serialized on PlayerCar.prefab - rename only with [FormerlySerializedAs].
[System.Serializable]
public class HandlingValues
{
    [Tooltip("Values For Crown Spawn Mechanic")]
    [Header("Spawn Rules")]
    [SerializeField, Min(0f)]
    public float minSpawnDistance = 5f;

    [Header("Crown Protection")]
    [SerializeField, Min(0f)]
    public float protectionSeconds = 3f;


    [Tooltip("Values For Collision")]
    [Header("Impact Classification")]
    [SerializeField, Min(0f)]
    public float minimumApproachSpeed = 1.5f; //m/s, below this speed, no contact is considered an impact

    [SerializeField, Min(0f)]
    public float aggressorAdvantage = 1.0f;//m/s, if the other car is not at least this much more responsible for closing the gap, no aggressor is assigned

    [SerializeField, Min(0f)]
    public float minimumContactNormalAlignment = 0.15f;//cosine of angle between contact normal and the line connecting the two cars' centers of mass. Below this, no aggressor is assigned.

    [Header("Slam")]
    [SerializeField, Min(0f)]
    public float slamMinimumSpeed = 3f;

    [Tooltip("Values For Power-Ups")]
    [Header("Grapple Field")]
    //[Tooltip("grapple_ticks (FD-04), in seconds: provisional 6, range 4 to 8.")]
    [SerializeField, Range(4f, 8f)]
    public float grappleDurationSeconds = 6f;

    [Tooltip("grapple_reach_bonus (FD-04): extra reach past the body collider, in metres. A near-miss, not an aura.")]
    [SerializeField, Min(0.01f)]
    public float grappleReachBonus = 0.5f;

    [Tooltip("Minimum relative speed (m/s) for a near-miss steal, so a car parked beside the holder can't steal.")]
    [SerializeField, Min(0f)]
    public float grappleMinimumRelativeSpeed = 1f;

    [Header("Damper Field")]
    [Tooltip("damper_ticks (FD-04), in seconds: provisional 5, range 3 to 7. Feels longest to the opponents - keep it short.")]
    [SerializeField, Range(3f, 7f)]
    public float damperDurationSeconds = 5f;

    // FD-04 gives no numbers for Repulse Pulse beyond its (low) roll weight - every value below is
    // provisional until playtest. The roll weight itself stays in PowerUpRoller.
    [Header("Repulse Pulse")]
    [Tooltip("Charge-up (telegraph) length, in seconds. Long enough for nearby drivers to react. Range 0.3 to 1.5.")]
    [SerializeField, Range(0.3f, 1.5f)]
    public float repulseChargeSeconds = 0.75f;

    [Tooltip("Reach of the pulse, in metres, measured flat from the user's centre.")]
    [SerializeField, Min(1f)]
    public float repulseRadius = 6f;

    [Tooltip("Velocity change (m/s) given to every car in range, away from the user. Equal for all - not scaled by distance.")]
    [SerializeField, Min(0f)]
    public float repulseShoveSpeed = 9f;

    [Tooltip("Seconds a shoved car has reduced grip (ShoveGripMultiplier) and can't be an aggressor.")]
    [SerializeField, Min(0f)]
    public float repulseShoveSeconds = 0.4f;

    [Tooltip("Cars further above or below the user than this (metres) don't count as nearby - ramp decks.")]
    [SerializeField, Min(0f)]
    public float repulseMaxHeightDifference = 3f;

    [Header("Repulse Pulse: being shoved")]
    [Tooltip("Multiplier on GripRate while a Repulse shove is carrying the car, so the push isn't scrubbed off by the tyres. Provisional.")]
    public float ShoveGripMultiplier = 0.1f;

    [Header("Longitudinal")]
    public float TopSpeed = 22f;              // m/s, nominal ceiling under throttle alone
    public float Acceleration = 14f;          // m/s²
    public float BrakeStrength = 26f;         // m/s²
    public float CoastDrag = 5f;              // m/s², off throttle
    public float ReverseTopSpeed = 8f;        // m/s
    public float ReverseDelay = 0.35f;        // s, brake held at rest before reverse engages
    public float OverspeedDecay = 3f;         // m/s², how fast speed above TopSpeed falls back

    [Header("Boost")]
    [Tooltip("Boost speed multiplier the drive model reads while its Boost input is set. PlayerMovement overwrites it " +
             "each tick on the runtime copy from Layer C (the stage / chain target), so this serialized value is only " +
             "the idle fallback.")]
    public float BoostMultiplier = 1.8f;      // raises target speed; no clamp at TopSpeed

    [Header("Steering")]
    public float MaxYawRate = 110f;           // deg/s
    public float SteerResponse = 0.18f;       // s, time to reach target yaw rate
    [Tooltip("Yaw authority vs |speed| / TopSpeed. Zero at a standstill (no pivoting in place).")]
    public AnimationCurve SpeedScalingCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 5f),
        new Keyframe(0.2f, 1f, 5f, -0.1875f),
        new Keyframe(1f, 0.85f, -0.1875f, 0f));

    [Header("Lateral")]
    [Tooltip("Sideways speed decay per second: exp(-rate * dt). ~20 = grippy.")]
    public float GripRate = 20f;
    [Tooltip("OBSOLETE: the Left Ctrl grip button is not in the Driving Foundation. PlayerMovement no longer " +
             "passes the Drift input to the drive model. Sliding grip is GripRate x DriftGripMultiplier.")]
    public float DriftGripRate = 3f;

    [Header("Airborne")]
    [Tooltip("Multiplier on gravity (the suspension carries the car while grounded).")]
    public float GravityScale = 2.2f;
    public float AirSteerMultiplier = 0.4f;   // never 0: the player can always steer
    public float AirDrag = 1.5f;              // m/s²

    [Header("Suspension")]
    [Tooltip("How far past rest height (= wheel radius) the probes look for ground.")]
    public float SuspensionTravel = 0.45f;
    [Tooltip("Spring stiffness around rest height. Changes bounciness, not ride height.")]
    public float SpringStrength = 55f;
    [Tooltip("Damping. Higher settles faster; too low bounces.")]
    public float DamperStrength = 6f;

    [Header("Attitude")]
    [Tooltip("How strongly the chassis aligns to the ground normal while grounded.")]
    public float GroundedLevelStrength = 6f;
    [Tooltip("How strongly it levels toward world up while airborne.")]
    public float AirLevelStrength = 2.5f;
    [Tooltip("Tilt (deg) beyond which drive input does nothing.")]
    public float MaxDriveAngle = 50f;

    [Header("Walls (doc 05)")]
    [Tooltip("Incidence angle (deg) separating a scrape from an impact.")]
    public float WallImpactAngle = 45f;
    [Tooltip("Speed fraction lost by a scrape at the impact angle (less at shallower angles).")]
    public float WallScrapeCost = 0.25f;
    [Tooltip("Speed fraction lost by an impact.")]
    public float WallImpactCost = 0.55f;
    [Tooltip("Push-off away from the wall on impact, m/s.")]
    public float WallReboundSpeed = 2f;
    [Tooltip("Impacts faster than this (m/s) trigger a spin-out.")]
    public float WallSpinoutSpeed = 16f;
    [Tooltip("Minimum speed after any wall contact, m/s (never above the arrival speed).")]
    public float MinExitSpeed = 4f;

    [Header("Spin-out recovery (doc 05)")]
    [Tooltip("Spin-out duration in ticks (45 ≈ 0.75 s).")]
    public int SpinoutTicks = 45;
    [Tooltip("Steering authority while spinning. Never zero.")]
    public float SpinoutSteerMultiplier = 0.35f;
    [Tooltip("Throttle authority while spinning.")]
    public float SpinoutThrottleMultiplier = 0.5f;
    [Tooltip("Ticks after a spin-out during which the car can't be spun again.")]
    public int ContactImmunityTicks = 45;

    [Header("Weight")]
    [Tooltip("Momentum transfer in car-vs-car contact. Not used yet.")]
    public float Mass = 1f;
    [Tooltip("Steering response multiplier. Never written to the Rigidbody's inertia tensor.")]
    public float Inertia = 1f;

    // ─── Layer C: Drift & Boost (doc 04) ────────────────────────────────
    // PROVISIONAL values from "Glowtag Drift, Boost & Overdrive: Implementation Answers v0.1".
    // Values marked (mine) are not in the designer's doc.

    [Header("Drift & Boost: hop and charge")]
    public float DriftMinSpeed = 6f;               // m/s
    public int HopTicks = 12;                      // ~0.2 s at 60 Hz
    [Tooltip("(mine) Upward m/s given at hop start. Tune so the hop lasts about HopTicks.")]
    public float HopVelocity = 1.2f;
    [Tooltip("(mine) Steer needed at landing to commit a drift.")]
    public float SteerCommitDeadzone = 0.2f;
    [Tooltip("Units/tick at optimal slip. Race value; Glowtag overrides back to it. Range 0-2.5.")]
    public float ReserveGainRate = 1f;
    public float OptimalSlipAngle = 22f;           // degrees
    [Tooltip("(mine) Slip angle at which charge bottoms out.")]
    public float MaxSlipAngle = 60f;
    [Tooltip("(mine) Charge scale at ~zero speed; the doc says speed 'scales mildly'.")]
    public float SpeedChargeFloor = 0.6f;
    public float Stage1Threshold = 45f;
    public float Stage2Threshold = 100f;
    public float Stage3Threshold = 170f;

    [Header("Drift & Boost: window and boost")]
    [Tooltip("~0.4 s at 60 Hz, range 10-45. The accessibility dial.")]
    public int WindowTicks = 24;
    public float Stage1BoostMultiplier = 1.12f;
    [Tooltip("Overdrive uses this stage.")]
    public float Stage2BoostMultiplier = 1.22f;
    public float Stage3BoostMultiplier = 1.35f;
    public int BoostTicks = 45;                    // ~0.75 s
    public int ChainGraceTicks = 30;               // ~0.5 s
    [Tooltip("Applies only while the aggressor is Boosting or Chained. Range 1.0-2.5.")]
    public float BoostBumpMultiplier = 1.6f;

    [Header("Drift & Boost: sliding handling")]
    [Tooltip("Multiplier on GripRate while Sliding. Automatic, not a button.")]
    public float DriftGripMultiplier = 0.45f;
    [Tooltip("Multiplier on MaxYawRate while Sliding (Arena preset value).")]
    public float DriftRotationAuthority = 1.35f;

    // ─── Surfaces (Handling Profiles) ───────────────────────────────────
    // Untagged geometry is Default (FD-06). Oil only exists as runtime patches from the Oil Slick
    // power-up (FD-04: "the sharper, briefer version of ice" - ice costs control, so oil costs grip).

    [Header("Surface: Oil")]
    [Tooltip("Multiplier on GripRate while the probe majority is on Oil. Low = the car keeps sliding the way it was going. Provisional.")]
    public float OilGripMultiplier = 0.15f;
    [Tooltip("Multiplier on steering authority while on Oil. Clamped above zero in code: steering authority is never zero (FD-04). Provisional.")]
    public float OilSteerAuthority = 0.6f;

    // ─── Bots (FD-07) ──────────────────────────────────────────────────
    // FD-07 gives some values in ticks at 60 Hz; per FD-05 Glowtag authors them in seconds and
    // converts at use (12 ticks = 0.2 s, 90 ticks = 1.5 s). Values marked "not in FD-07" are provisional.

    [Header("Bots")]
    [Tooltip("bot_reaction_ticks, in seconds: how often a bot re-picks its mode and target. Higher = slower, easier to escape. FD-07: 0.2 s (range ~0.07 to 0.5).")]
    [SerializeField, Min(0.02f)]
    public float botReactionSeconds = 0.2f;

    [Tooltip("bot_aim_error: constant angular offset on the steer target, in degrees. Higher wanders more. Range 0 to 10.")]
    [SerializeField, Range(0f, 10f)]
    public float botAimErrorDegrees = 3f;

    [Tooltip("bot_steer_full_angle: target angle (degrees) at which steering reaches full lock. Range 25 to 70.")]
    [SerializeField, Range(25f, 70f)]
    public float botSteerFullAngle = 45f;

    [Tooltip("bot_whisker_length, in chassis lengths (~1.5).")]
    [SerializeField, Min(0.1f)]
    public float botWhiskerChassisLengths = 1.5f;

    [Tooltip("Angle of the forward-left / forward-right whiskers from straight ahead, degrees. Not in FD-07.")]
    [SerializeField, Range(10f, 60f)]
    public float botWhiskerSideAngle = 30f;

    [Tooltip("bot_crate_angle: a bot takes a crate only if it's already within this many degrees of its heading. Range 10 to 45.")]
    [SerializeField, Range(10f, 45f)]
    public float botCrateAngle = 25f;

    [Tooltip("bot_stuck_ticks, in seconds: below botStuckSpeed this long = Recover. FD-07: 1.5 s (range 0.75 to 3).")]
    [SerializeField, Min(0.1f)]
    public float botStuckSeconds = 1.5f;

    [Tooltip("stuck_speed: below this horizontal speed (m/s) a bot counts as stuck. Not in FD-07 (foundation value).")]
    [SerializeField, Min(0f)]
    public float botStuckSpeed = 1f;

    [Tooltip("How long Recover lasts once entered, in seconds (it also lasts while spun out). Not in FD-07.")]
    [SerializeField, Min(0.1f)]
    public float botRecoverSeconds = 1f;

    [Tooltip("Run mode: how far past itself, directly away from the nearest rival, the bot aims (metres). Not in FD-07.")]
    [SerializeField, Min(1f)]
    public float botRunDistance = 15f;

    [Tooltip("Recover target: the arena centre, X and Z (FD-07). The default arena is centred on the origin.")]
    public float botArenaCenterX = 0f;
    public float botArenaCenterZ = 0f;
}