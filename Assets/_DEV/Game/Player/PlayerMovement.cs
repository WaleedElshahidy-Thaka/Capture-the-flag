using UnityEngine;
using Fusion;

// Layer B of the Driving Foundation - the per-tick physics of one robot chassis, built to GDD
// doc 03 (Vehicle Drive Model). Phase 1 scope: M1 (chassis + probes + suspension), M2
// (longitudinal), M4 (steering + inertia). Slope (M3), surfaces and the airborne polish pass
// come later; drift and boost (doc 04) are stubbed at their neutral values.
//
// Three structural decisions come from the doc rather than from preference, and all three are
// the reason the earlier BoxCollider / WheelCollider / AddForce attempts kept missing:
//
//   1. Custom arcade controller, not WheelCollider and not "a general rigidbody with friction
//      materials doing the work". Boost has to carry the chassis above nominal top speed, which
//      a modelled powertrain resists, and PhysX's internal solver state doesn't survive a
//      rollback.
//   2. One rigid body with four raycast ground probes, not four simulated wheels.
//   3. Velocity is composed and written per tick, not accumulated through the engine's
//      integrator - so a re-simulated tick from a restored state lands on the same result.
//
// The chassis collider stays for car-vs-car and wall contact; PhysX detects those, and doc 05
// (Collision, Contact & Recovery) will classify and resolve them in Phase 2. Until then wall
// contact is crude, because a direct velocity write partly overwrites PhysX's own response -
// that's expected, and it's what Phase 2 exists to replace rather than something to patch here.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMatchState))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] HandlingValues handling = new HandlingValues();
    [SerializeField] LayerMask groundMask = ~0;

    [Header("Probe geometry (baked from the art by GameSceneSetup - don't hand-edit)")]
    [Tooltip("Local positions of the four wheels: front-left, front-right, rear-left, rear-right.")]
    [SerializeField] Vector3[] probeOffsets = new Vector3[4];
    [Tooltip("Measured from the wheel mesh. Doubles as the suspension's rest height, so probes hold the car exactly where the wheel colliders touch.")]
    [SerializeField] float wheelRadius = 0.3f;

    [Header("Tire visuals (cosmetic only - purely for looking right)")]
    [SerializeField] Transform visual; // parent of the 4 tires - rotated for lean, never the Rigidbody
    [SerializeField] Transform frontLeftTire;
    [SerializeField] Transform frontRightTire;
    [SerializeField] Transform rearLeftTire;
    [SerializeField] Transform rearRightTire;
    [SerializeField] float maxTireSteerAngle = 30f;

    [Header("Cosmetic body lean (visual only - the Rigidbody never pitches or rolls)")]
    [SerializeField] float maxLeanAngle = 12f;
    [SerializeField] float leanSpring = 8f;

    // Accumulating simulation state, so per doc 03's determinism contract it is replicated and
    // restored on rollback like anything else that carries across ticks.
    [Networked] float YawRate { get; set; }            // degrees/second
    [Networked] float BrakeHoldSeconds { get; set; }   // brake-to-reverse dwell
    // Cosmetic, but every peer needs it to draw remote wheels turning - a plain field is only
    // ever written on the peer that owns the car.
    [Networked] float SteerInput { get; set; }

    public bool IsGrounded { get; private set; }
    public float ForwardSpeed { get; private set; }

    Rigidbody body;
    PlayerMatchState matchState;
    ProbeResult[] probes = new ProbeResult[4];
    float tireRollAngle;
    Quaternion visualLeanRotation = Quaternion.identity;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        matchState = GetComponent<PlayerMatchState>();

        // Rotation constraints are deliberately NOT set here. An earlier version froze pitch and
        // roll to guarantee doc 03's "the chassis never lands upside down" - but freezing also
        // stops it conforming to slopes, and it silently overwrote whatever was set on the
        // prefab. The no-rollover rule is now met by actively levelling the chassis (see
        // ResolveAttitude) rather than by forbidding the rotation outright, which is what the
        // doc actually asks for: "force-levelled", not "unable to rotate".

        // Frictionless on purpose. The drive model owns grip - lateral hold is LateralGrip in
        // the lateral step, not a PhysicsMaterial - so any friction here would be a second,
        // unmodelled force fighting the composed velocity. Doc 03 rules out "friction materials
        // doing the work" explicitly.
        var frictionless = new PhysicsMaterial("ChassisFrictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
        };
        foreach (var collider in GetComponents<Collider>())
            collider.material = frictionless;
    }

    // HasInputAuthority, not HasStateAuthority - under Host/Client the host holds State Authority
    // over every car, so HasStateAuthority means "I am the host", not "this is my car".
    public override void Spawned()
    {
        if (Object.HasInputAuthority == false) return;

        var cameraGO = GameObject.Find("Main Camera");
        if (cameraGO != null && cameraGO.TryGetComponent(out PlayerCamera cam))
            cam.SetTarget(body);
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerNetInput input) == false) return;
        SimulateTick(input, Runner.DeltaTime);
    }

    // Public and input-struct-driven on purpose. Glowtag FD-07: "A bot is an input source, not a
    // second movement system" - the authority will synthesise a PlayerNetInput per bot each tick
    // and feed it through this same entry point. Bots have no PlayerRef and so never arrive via
    // GetInput, which is why the simulation can't be buried inside FixedUpdateNetwork.
    public void SimulateTick(PlayerNetInput input, float deltaTime)
    {
        // Step 2: ground probe. Rest height is the wheel radius, so the suspension holds the
        // chassis exactly where the wheel colliders would touch - the two support systems agree
        // rather than fighting each other for the same contact.
        GroundProbes.Cast(transform, body, probeOffsets, wheelRadius + handling.SuspensionTravel,
                          wheelRadius, groundMask, probes);

        Vector3 groundNormal = GroundProbes.AverageGroundedNormal(probes, Vector3.up);
        bool hasContact = GroundProbes.CountGrounded(probes) >= 2;

        // Grounded requires being the right way up as well as touching something. Without this a
        // car resting on its side still finds "ground" with its sideways-pointing probes and
        // happily drives along the floor on its shoulder.
        float tilt = Vector3.Angle(transform.up, groundNormal);
        IsGrounded = hasContact && tilt <= handling.MaxDriveAngle;

        // Step 3 (slope) and step 4 (profile composition) arrive with M3 and the profile layer.
        // Reading the body's current velocity rather than a remembered one means anything PhysX
        // did to us between ticks - a wall, another car - is carried into this tick instead of
        // being silently erased by the write at the end.
        Vector3 velocity = body.linearVelocity;

        // Steps 5-8 grounded, step 9 airborne.
        velocity = IsGrounded
            ? ResolveGrounded(input, velocity, deltaTime)
            : ResolveAirborne(input, velocity, deltaTime);

        SteerInput = input.SteerAxis;

        // Step 10: write velocity and angular velocity.
        body.linearVelocity = velocity;
        body.angularVelocity = ResolveAttitude(hasContact, groundNormal);

        // Step 11 (contact) resolves after this write, once doc 05 exists - never during.
    }

    // Pitch and roll are levelled rather than frozen. Grounded, the chassis aligns to the ground
    // normal, which is what makes it sit into a slope or a banked curve instead of staying
    // stubbornly flat. Airborne, it levels toward world up more gently, which is doc 03's
    // "force-levelled in air and always lands on its tyres" - the rule exists because an
    // upside-down chassis is a state a young player can't read or recover from, and Glowtag has
    // no respawn to rescue them with.
    //
    // Composed as an angular velocity rather than a torque for the same reason the linear step
    // writes velocity directly: the drive model owns its integration. Yaw comes from the
    // steering model and is applied around the chassis's own up axis, so levelling and steering
    // don't fight over the same component.
    Vector3 ResolveAttitude(bool hasContact, Vector3 groundNormal)
    {
        Vector3 targetUp = hasContact ? groundNormal : Vector3.up;
        float strength = hasContact ? handling.GroundedLevelStrength : handling.AirLevelStrength;

        // Cross product gives the axis to rotate about, scaled by the sine of the error - so it
        // falls to zero as the chassis comes level, with no overshoot to damp.
        Vector3 levelling = Vector3.Cross(transform.up, targetUp) * strength;

        return levelling + transform.up * (YawRate * Mathf.Deg2Rad);
    }

    Vector3 ResolveGrounded(PlayerNetInput input, Vector3 velocity, float dt)
    {
        Vector3 local = transform.InverseTransformDirection(velocity);

        // Step 5: suspension. Spring and damper per grounded probe, resolved as a vertical
        // acceleration rather than as AddForceAtPosition - the model owns its own integration,
        // and doc 03 treats chassis lean as visual weight at no gameplay cost, so the asymmetry
        // between corners drives the cosmetic tilt in LateUpdate instead of real body rotation.
        float suspension = GroundProbes.SuspensionResponse(probes, handling.SpringStrength, handling.DamperStrength);
        float vertical = local.y + (suspension + Physics.gravity.y * handling.GravityScale) * dt;

        // Step 6: longitudinal.
        float forwardSpeed = ResolveLongitudinal(input, local.z, dt);
        ForwardSpeed = forwardSpeed;

        // Step 7: steering.
        ResolveSteering(input, forwardSpeed, dt, authority: 1f);

        // Step 8: lateral. The fraction of sideways velocity removed per tick's worth of time -
        // the gap between where the chassis points and where it's going is the slide, and every
        // drift is that gap being opened deliberately. Scrub is not returned to the chassis.
        float driftAuthority = input.Drift ? handling.DriftGripMultiplier : 1f;
        float grip = Mathf.Clamp01(handling.LateralGrip * driftAuthority);
        float lateral = local.x * (1f - grip);

        return transform.TransformDirection(new Vector3(lateral, vertical, forwardSpeed));
    }

    float ResolveLongitudinal(PlayerNetInput input, float forwardSpeed, float dt)
    {
        bool throttle = input.ThrottleAxis > 0.01f;
        bool brake = input.ThrottleAxis < -0.01f;

        // Boost raises the target without clamping at nominal top speed. Placeholder until doc
        // 04 supplies a real boost contribution - the no-clamp rule is the part that matters,
        // since a hard ceiling would make a second boost taken while boosting worth nothing.
        float targetTopSpeed = handling.TopSpeed * (input.Boost ? handling.BoostMultiplier : 1f);

        float target;
        float rate;

        if (throttle && !brake)
        {
            target = targetTopSpeed;
            rate = handling.Acceleration;
            BrakeHoldSeconds = 0f;
        }
        else if (brake)
        {
            if (forwardSpeed > 0.1f)
            {
                // Braking while still moving forward - reverse is not engaged yet.
                target = 0f;
                rate = handling.BrakeStrength;
                BrakeHoldSeconds = 0f;
            }
            else
            {
                // At rest or nearly so: dwell before engaging reverse, so a tap at low speed
                // doesn't reverse by accident.
                BrakeHoldSeconds += dt;
                bool reverseEngaged = BrakeHoldSeconds >= handling.ReverseDelay;
                target = reverseEngaged ? -handling.ReverseTopSpeed : 0f;
                rate = reverseEngaged ? handling.Acceleration : handling.BrakeStrength;
            }
        }
        else
        {
            target = 0f;
            rate = handling.CoastDrag;
            BrakeHoldSeconds = 0f;
        }

        // The chaining rule: above nominal top speed, decay is deliberately gentle so a boost
        // chain can hold the chassis above the ceiling. Braking is exempt - player intent should
        // still stop the car - which is a small deviation from reading the doc's table literally,
        // noted here because it's a decision rather than an oversight.
        bool coastingAboveCeiling = !brake
                                    && Mathf.Abs(forwardSpeed) > handling.TopSpeed
                                    && Mathf.Abs(forwardSpeed) > Mathf.Abs(target);
        if (coastingAboveCeiling) rate = handling.OverspeedDecay;

        return Mathf.MoveTowards(forwardSpeed, target, rate * dt);
    }

    // The steer axis never rotates the chassis directly - it sets a target yaw rate which the
    // chassis approaches over time, and how fast it approaches is what makes one robot feel
    // nimble and another heavy. Inertia is a plain multiplier here and is deliberately never
    // written into the Rigidbody's inertia tensor: that would hand it back to PhysX's solver and
    // re-couple it to mass, which doc 03 forbids for both determinism and authoring reasons.
    void ResolveSteering(PlayerNetInput input, float forwardSpeed, float dt, float authority)
    {
        float speedRatio = handling.TopSpeed > 0f ? Mathf.Abs(forwardSpeed) / handling.TopSpeed : 0f;
        float speedScale = handling.SpeedScalingCurve?.Evaluate(Mathf.Clamp01(speedRatio)) ?? 1f;

        const float driftRotationAuthority = 1f; // neutral until Drift & Boost (doc 04) exists

        float targetYawRate = input.SteerAxis * handling.MaxYawRate * speedScale * driftRotationAuthority * authority;

        float responseTime = Mathf.Max(0.001f, handling.SteerResponse * handling.Inertia);
        float alpha = 1f - Mathf.Exp(-dt / responseTime);
        YawRate += (targetYawRate - YawRate) * alpha;
    }

    // Steps 5 through 8 are replaced entirely while airborne. Steering authority is reduced but
    // never zero - no state in this foundation leaves the player unable to steer.
    Vector3 ResolveAirborne(PlayerNetInput input, Vector3 velocity, float dt)
    {
        Vector3 local = transform.InverseTransformDirection(velocity);

        ResolveSteering(input, local.z, dt, authority: handling.AirSteerMultiplier);
        ForwardSpeed = local.z;

        // Air drag is gentler than coast drag - a jump shouldn't cost the player their speed.
        Vector3 horizontal = new Vector3(local.x, 0f, local.z);
        float horizontalSpeed = Mathf.MoveTowards(horizontal.magnitude, 0f, handling.AirDrag * dt);
        horizontal = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized * horizontalSpeed : Vector3.zero;

        // Gravity is scaled above real gravity for arcade weight and a fast return to ground.
        float vertical = local.y + Physics.gravity.y * handling.GravityScale * dt;

        return transform.TransformDirection(new Vector3(horizontal.x, vertical, horizontal.z));
    }

    // Purely cosmetic, and deliberately so - doc 03 counts chassis lean as "visual weight, at no
    // gameplay cost". Tires roll from real forward speed and steer from the replicated input;
    // the body tilts from the difference in suspension compression across the probes, which is
    // now real per-corner data rather than an approximation.
    void LateUpdate()
    {
        tireRollAngle += (ForwardSpeed / wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;
        tireRollAngle %= 360f;

        float steerAngle = SteerInput * maxTireSteerAngle;

        SetTire(frontLeftTire, steerAngle);
        SetTire(frontRightTire, steerAngle);
        SetTire(rearLeftTire, 0f);
        SetTire(rearRightTire, 0f);

        if (visual == null) return;

        float left = (probes[0].Compression + probes[2].Compression) * 0.5f;
        float right = (probes[1].Compression + probes[3].Compression) * 0.5f;
        float front = (probes[0].Compression + probes[1].Compression) * 0.5f;
        float rear = (probes[2].Compression + probes[3].Compression) * 0.5f;

        Quaternion target = Quaternion.Euler((rear - front) * maxLeanAngle, 0f, -(right - left) * maxLeanAngle);
        float alpha = 1f - Mathf.Exp(-leanSpring * Time.deltaTime);
        visualLeanRotation = Quaternion.Slerp(visualLeanRotation, target, alpha);
        visual.localRotation = visualLeanRotation;
    }

    void SetTire(Transform tire, float steerAngle)
    {
        if (tire == null) return;
        tire.localRotation = Quaternion.Euler(tireRollAngle, steerAngle, 0f);
    }
}
