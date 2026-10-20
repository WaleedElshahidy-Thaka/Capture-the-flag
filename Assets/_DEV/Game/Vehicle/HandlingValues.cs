using UnityEngine;

// Every tuning value the drive model reads (GDD doc 03 tuning table). A plain serializable
// class so it can later be wrapped in a ScriptableObject handling profile without touching callers.
// Field names are serialized on PlayerCar.prefab - rename only with [FormerlySerializedAs].
[System.Serializable]
public class HandlingValues
{
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
}