using UnityEngine;

// Every feel value the drive model reads, in one place. Provisional numbers and ranges are
// taken straight from GDD doc 03's tuning table - they're chosen to be legible rather than
// correct, and the doc says so.
//
// Serialized fields for now, by decision: authoring a ScriptableObject schema for numbers nobody
// has tuned yet is work done in the wrong order. This is a plain [Serializable] class rather
// than fields scattered across PlayerMovement precisely so that lifting it into a
// ScriptableObject later is a wrapper around this type, not a rewrite of everything that reads
// it (doc 03: "every value lives in a handling profile ScriptableObject, not in code").
//
// Doc 03's own note applies: the relationship between top speed and arena scale is circular and
// can't be resolved on paper. Fix top speed, build one arena against it, tune the arena.
// ArenaBounds.CrossingSeconds reports where that currently stands.
[System.Serializable]
public class HandlingValues
{
    [Header("Longitudinal")]
    public float TopSpeed = 22f;              // m/s, nominal ceiling under throttle alone, level ground
    public float Acceleration = 14f;          // m/s², approach rate to target speed
    public float BrakeStrength = 26f;         // m/s²
    public float CoastDrag = 5f;              // m/s², deceleration off throttle
    public float ReverseTopSpeed = 8f;        // m/s, deliberately well under forward
    public float ReverseDelay = 0.35f;        // s, brake-to-reverse dwell, stops accidental reverse
    public float OverspeedDecay = 3f;         // m/s², how fast speed above the ceiling falls back

    [Header("Boost")]
    [Tooltip("Placeholder until Drift & Boost (doc 04) exists - that layer supplies the real boost contribution.")]
    public float BoostMultiplier = 1.8f;      // raises target speed; no clamp at nominal top speed

    [Header("Steering")]
    public float MaxYawRate = 110f;           // deg/s, turning authority at reference speed
    public float SteerResponse = 0.18f;       // s, base time to reach target yaw rate
    [Tooltip("Yaw authority against speed ratio (|forward speed| / TopSpeed). Zero at a standstill - the car can't pivot in place, steering needs motion like a real car - full by 20% of top speed, eased back a little at the top to cut twitchiness.")]
    public AnimationCurve SpeedScalingCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 5f),
        new Keyframe(0.2f, 1f, 5f, -0.1875f),
        new Keyframe(1f, 0.85f, -0.1875f, 0f));

    [Header("Lateral")]
    [Tooltip("How fast sideways velocity is scrubbed off, per second. Sideways speed decays as exp(-rate * dt), so it's independent of tick rate. ~20 means 90% gone in about 0.12s: goes where it points, with a small slide on hard corners.")]
    public float GripRate = 20f;
    [Tooltip("Same, while the drift button (Left Ctrl) is held. ~3 means the slide lingers for a second or so - the chassis visibly goes sideways. Placeholder until Drift & Boost (doc 04) owns the drift state machine.")]
    public float DriftGripRate = 3f;

    [Header("Airborne")]
    [Tooltip("Multiplier on real gravity while airborne (the suspension carries the chassis while grounded). Above 1 for arcade weight and a fast return to ground.")]
    public float GravityScale = 2.2f;
    public float AirSteerMultiplier = 0.4f;   // never 0 - doc 03 Design Goal 6
    public float AirDrag = 1.5f;              // m/s², gentler than coast drag

    [Header("Suspension (ride height - doc 03 marks these visual, tune late)")]
    [Tooltip("How far past the wheel's resting contact the probe keeps looking. Rest height itself comes from the measured wheel radius, so the suspension holds the car exactly where its wheel colliders just touch.")]
    public float SuspensionTravel = 0.45f;
    [Tooltip("Stiffness of the hold around rest height (m/s² per rest-length of displacement). Changes how bouncy the ride is, never where it sits - the spring is preloaded, gravity is carried at zero displacement.")]
    public float SpringStrength = 55f;
    [Tooltip("Damping of that hold (per second of vertical speed). Higher settles faster; too low bounces after landings and bumps.")]
    public float DamperStrength = 6f;

    [Header("Attitude")]
    [Tooltip("How strongly the chassis aligns to the ground normal while grounded. This is what conforms it to slopes.")]
    public float GroundedLevelStrength = 6f;
    [Tooltip("How strongly it levels toward world up while airborne. Doc 03: the chassis always lands on its tyres - there is no rollover state in this foundation.")]
    public float AirLevelStrength = 2.5f;
    [Tooltip("Tilt beyond this angle from the ground normal means drive input does nothing - a car on its side can't drive along the floor.")]
    public float MaxDriveAngle = 50f;

    [Header("Weight (doc 03 keeps these independent on purpose)")]
    [Tooltip("Momentum transfer during contact only. Consumed by Collision & Recovery (doc 05), not used here yet.")]
    public float Mass = 1f;
    [Tooltip("Steering response multiplier only. Never written into the Rigidbody's inertia tensor.")]
    public float Inertia = 1f;
}
