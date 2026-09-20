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
// Runs before RunnerSimulatePhysics (order 0) within the same tick, so the velocity written at
// step 10 is what PhysX integrates for this tick rather than the next. Fusion 2 orders
// FixedUpdateNetwork by Unity's script execution order.
[DefaultExecutionOrder(-50)]
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

    // Recovery state (doc 05 RecoveryState). Networked, not a local effect: "a locally-triggered
    // spin that the authority disagrees with is the exact desync players notice most."
    // SettleHeading is the direction of travel at entry, in world yaw degrees; SpinDegrees is
    // the signed rotation the spin covers on its way there, so the chassis lands facing where
    // it's going and never emerges backwards.
    [Networked] int SpinoutTicksRemaining { get; set; }
    [Networked] int ImmunityTicksRemaining { get; set; }
    [Networked] float SettleHeading { get; set; }
    [Networked] float SpinDegrees { get; set; }

    public bool IsSpinningOut => SpinoutTicksRemaining > 0;
    public bool HasContactImmunity => ImmunityTicksRemaining > 0;

    public bool IsGrounded { get; private set; }
    public float ForwardSpeed { get; private set; }

    // Below this, a residual velocity component is treated as zero so a parked car is
    // actually parked (m/s).
    const float RestDeadband = 0.01f;

    Rigidbody body;
    PlayerMatchState matchState;
    ProbeResult[] probes = new ProbeResult[4];
    float tireRollAngle;
    Quaternion visualLeanRotation = Quaternion.identity;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        matchState = GetComponent<PlayerMatchState>();

        // No rotation constraints - enforced here in code, not left to the prefab. An earlier
        // version froze pitch and roll to guarantee doc 03's "the chassis never lands upside
        // down", but freezing stops the chassis conforming to slopes, and it makes PhysX cancel
        // ResolveAttitude's levelling every step (the rotation jitter seen in the inspector).
        // The no-rollover rule is met by actively levelling instead, which is what the doc
        // actually asks for: "force-levelled", not "unable to rotate". Clearing it here means a
        // prefab that still carries stale constraints can't silently reintroduce the bug.
        body.constraints = RigidbodyConstraints.None;

        // The model owns gravity (GravityScale, step 5 / step 9) - PhysX must not add its own on
        // top. With both on, gravity was applied twice, and PhysX's share pulled the frictionless
        // chassis down any slope the model wasn't modelling, so a parked car crept downhill.
        body.useGravity = false;

        // No engine damping either. PhysX's linear damping removed a fixed fraction of velocity
        // every step, which capped the car at ~9 m/s whatever TopSpeed said (the drag per step
        // outgrew Acceleration's gain per step at that speed). Drag is CoastDrag / AirDrag in the
        // model; a second, hidden drag is exactly the kind of unmodelled force doc 03 rules out.
        body.linearDamping = 0f;
        body.angularDamping = 0f;

        // Never sleep. Velocity is authored every tick; a body PhysX has put to sleep ignores
        // the simulation step entirely, and the write that would wake it is the one being
        // skipped.
        body.sleepThreshold = 0f;

        // Frictionless on purpose. The drive model owns grip - lateral hold is GripRate in
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

    // The camera binds to whichever car is ours - HasInputAuthority, not HasStateAuthority, since
    // under Host/Client the host holds State Authority over every car. Polled rather than done
    // in Spawned: after a host migration our car comes back ownerless and is handed to us a
    // moment later, and Fusion has no callback for that hand-over.
    bool cameraBound;

    public override void Render()
    {
        bool mine = Object.HasInputAuthority;
        if (mine == cameraBound) return;
        cameraBound = mine;
        if (!mine) return;

        // The camera follows the View child NetworkRigidbody interpolates, not the physics root.
        var networkRigidbody = GetComponent<Fusion.Addons.Physics.NetworkRigidbody>();
        Transform view = networkRigidbody != null ? networkRigidbody.InterpolationTarget : null;

        var cameraGO = GameObject.Find("Main Camera");
        if (cameraGO != null && cameraGO.TryGetComponent(out PlayerCamera cam))
            cam.SetTarget(body, view, matchState);
    }

    // Three sources of input, one pipeline:
    //   - a player: their PlayerNetInput arrives via GetInput on the host and on their own client
    //   - a bot (no input authority - the player left, or hasn't come back after a migration):
    //     the host asks BotDriver; clients get the result by replication like any proxy
    //   - CanMove not yet released: neutral input, so the car still settles onto its suspension
    //     in the lobby instead of hanging at spawn height
    // And one override: while the session is frozen after a host migration, every car holds
    // still where the snapshot put it until the shared resume tick.
    public override void FixedUpdateNetwork()
    {
        var session = MatchmakingSessionState.Local;
        if (session != null && session.Frozen)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            return;
        }

        PlayerNetInput input;
        if (GetInput(out input) == false)
        {
            if (!Object.HasStateAuthority || !matchState.IsBot) return;
            input = BotDriver.Think(this);
        }

        SimulateTick(matchState.CanMove ? input : default, Runner.DeltaTime);
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

        // Recovery first: a spin-out reduces the input the rest of the tick sees, and supplies
        // its own yaw on top of the player's.
        float spinYawRate = TickRecovery(ref input, deltaTime);

        // Steps 5-8 grounded, step 9 airborne. A spinning chassis slides instead of driving -
        // the longitudinal/lateral split is meaningless while the nose is sweeping round.
        if (IsSpinningOut) velocity = ResolveSpinning(input, velocity, deltaTime);
        else velocity = IsGrounded
            ? ResolveGrounded(input, velocity, deltaTime)
            : ResolveAirborne(input, velocity, deltaTime);

        SteerInput = input.SteerAxis;

        // Step 11, walls: the drive model has composed its velocity; if that velocity meets a
        // wall this tick, WallContact says what to write instead. Resolved before the write
        // rather than after PhysX has bounced the chassis - the point is that PhysX never gets
        // to. Chassis-to-chassis contact (host-resolved, doc 05 M1/M2) arrives in Phase 2b/2c.
        velocity = ResolveWallContact(velocity, deltaTime);

        // Step 10: write velocity and angular velocity.
        body.linearVelocity = velocity;
        body.angularVelocity = ResolveAttitude(hasContact, groundNormal, spinYawRate);
    }

    // ─── Recovery: spin-out (doc 05 M4) ─────────────────────────────────

    // Entered by a wall impact above WallSpinoutSpeed (here) or a slam (Phase 2c). Refused
    // while already spinning or immune - the chassis can still be pushed, its timer never
    // extends. travelDirection is where the chassis is going as it enters; that is where it
    // will be facing when it comes out. spinSign picks the turn direction (+1 clockwise).
    public void EnterSpinout(Vector3 travelDirection, float spinSign)
    {
        if (IsSpinningOut || HasContactImmunity) return;

        Vector3 travel = new Vector3(travelDirection.x, 0f, travelDirection.z);
        if (travel.sqrMagnitude < 0.0001f) travel = transform.forward;
        float settle = Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg;
        float current = transform.eulerAngles.y;

        // One full turn in the spin direction, plus whatever it takes to land on the settle
        // heading - between half a turn and one and a half, so it always reads as a spin and
        // never as a twitch.
        float sign = spinSign >= 0f ? 1f : -1f;
        float toSettle = Mathf.DeltaAngle(current, settle);
        if (sign > 0f && toSettle < 0f) toSettle += 360f;
        if (sign < 0f && toSettle > 0f) toSettle -= 360f;
        float total = toSettle + sign * 360f;
        if (Mathf.Abs(total) > 540f) total -= sign * 360f;

        SpinoutTicksRemaining = Mathf.Max(1, handling.SpinoutTicks);
        SettleHeading = settle;
        SpinDegrees = total;
        YawRate = 0f;

        Debug.Log($"[Spinout] enter: {total:0}° over {SpinoutTicksRemaining} ticks, settling at {settle:0}°");
    }

    // Counts the state down, scales the input, and returns the yaw rate the spin itself needs
    // this tick. The spin follows an eased curve from entry heading to settle heading - fast in
    // the middle, slowing into the landing - so it looks like a controlled rotation, not a
    // top. Driven as a target yaw per tick rather than a fixed rate, so a hit mid-spin that
    // pushes the chassis still ends with it facing its travel direction.
    float TickRecovery(ref PlayerNetInput input, float dt)
    {
        if (ImmunityTicksRemaining > 0) ImmunityTicksRemaining--;
        if (!IsSpinningOut) return 0f;

        input.SteerAxis *= handling.SpinoutSteerMultiplier;
        input.ThrottleAxis *= handling.SpinoutThrottleMultiplier;
        input.Drift = false;

        int total = Mathf.Max(1, handling.SpinoutTicks);
        float fractionNext = 1f - (SpinoutTicksRemaining - 1) / (float)total;
        float eased = fractionNext * fractionNext * (3f - 2f * fractionNext); // smoothstep
        float desiredYaw = SettleHeading - (1f - eased) * SpinDegrees;
        float yawRate = Mathf.DeltaAngle(transform.eulerAngles.y, desiredYaw) / dt;

        SpinoutTicksRemaining--;
        if (SpinoutTicksRemaining == 0)
        {
            ImmunityTicksRemaining = handling.ContactImmunityTicks;
            Debug.Log($"[Spinout] exit: facing {transform.eulerAngles.y:0}°, immune for {ImmunityTicksRemaining} ticks");
        }

        return yawRate;
    }

    // While spinning the chassis is a puck: it keeps sliding the way it was going, bleeding
    // speed at coast drag, held up by the suspension as usual. The reduced throttle still adds
    // a little push along wherever the nose happens to point - throttle authority is reduced,
    // not removed - and the reduced steering still feeds YawRate through ResolveSteering.
    Vector3 ResolveSpinning(PlayerNetInput input, Vector3 velocity, float dt)
    {
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float speed = Mathf.MoveTowards(horizontal.magnitude, 0f, handling.CoastDrag * dt);
        horizontal = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized * speed : Vector3.zero;

        Vector3 nose = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        horizontal += nose * (input.ThrottleAxis * handling.Acceleration * dt);
        if (horizontal.magnitude > handling.TopSpeed) horizontal = horizontal.normalized * handling.TopSpeed;

        ForwardSpeed = Vector3.Dot(horizontal, nose);
        ResolveSteering(input, ForwardSpeed, dt, authority: 1f);

        float vertical;
        if (IsGrounded)
        {
            float suspension = GroundProbes.SuspensionResponse(probes, handling.SpringStrength, handling.DamperStrength);
            vertical = velocity.y + suspension * dt;
        }
        else
        {
            vertical = velocity.y + Physics.gravity.y * handling.GravityScale * dt;
        }

        return new Vector3(horizontal.x, vertical, horizontal.z);
    }

    // Three rays along the direction of travel - centre and both front corners - as far as the
    // chassis will move this tick plus its own half-length. Direction flattened to horizontal:
    // walls stand upright, and it keeps a suspension bob from grazing the floor instead. Any
    // hit with a sideways normal is a wall (WallContact.IsWall); the nearest decides. PhysX
    // stays underneath as the backstop for whatever three rays miss.
    //
    // Three rays, not a box sweep: a sweep started inside its own collider reports the wall
    // with a garbage normal on the tick after first contact, which is exactly the continuous
    // scrape this has to keep resolving. Rays don't have that problem.
    const float WallProbeHalfWidth = 0.45f;   // half the chassis box (GameSceneSetup.BuildColliders)
    const float WallProbeHalfLength = 0.55f;
    const float WallProbeSkin = 0.05f;
    bool touchingWall;

    Vector3 ResolveWallContact(Vector3 velocity, float dt)
    {
        Vector3 heading = new Vector3(velocity.x, 0f, velocity.z);
        float speed = heading.magnitude;
        if (speed < 0.01f) { touchingWall = false; return velocity; }
        heading /= speed;

        Vector3 origin = transform.position + Vector3.up * wheelRadius;
        Vector3 side = Vector3.Cross(Vector3.up, heading) * WallProbeHalfWidth;
        float reach = WallProbeHalfLength + speed * dt + WallProbeSkin;

        bool found = false;
        RaycastHit nearest = default;
        for (int i = -1; i <= 1; i++)
        {
            if (!Physics.Raycast(origin + side * i, heading, out var hit, reach, groundMask, QueryTriggerInteraction.Ignore)) continue;
            // Static geometry only: our own wheel spheres sit right in a corner ray's path, and
            // another car's box has wall-like side normals - both carry a Rigidbody, walls don't.
            if (hit.rigidbody != null) continue;
            if (!WallContact.IsWall(hit.normal)) continue;
            if (found && hit.distance >= nearest.distance) continue;
            nearest = hit;
            found = true;
        }

        // Side hint for a dead-on hit: whichever way the nose is already leaning along the wall.
        Vector3 sideHint = Vector3.Dot(transform.forward, Vector3.Cross(nearest.normal, Vector3.up)) >= 0f
            ? Vector3.Cross(nearest.normal, Vector3.up)
            : Vector3.Cross(Vector3.up, nearest.normal);

        if (!found || !WallContact.TryResolve(velocity, nearest.normal, sideHint, handling, out var contact))
        {
            touchingWall = false;
            return velocity;
        }

        // One line per contact, not per tick of a scrape - the classification is what's being
        // checked here, and this is the log 2b extends to chassis contact.
        if (!touchingWall)
            Debug.Log($"[WallContact] {contact.Class} at {contact.IncidenceDegrees:0}°, {contact.ClosingSpeed:0.0} m/s in -> {contact.Velocity.magnitude:0.0} m/s out{(contact.QualifiesForSpinout ? " (spin-out speed)" : "")}");
        touchingWall = true;

        // A hard enough impact spins the chassis - turning toward the side it's now sliding to,
        // settling facing that slide. EnterSpinout refuses if already spinning or immune.
        if (contact.QualifiesForSpinout)
            EnterSpinout(contact.Velocity, Vector3.Dot(contact.Velocity, transform.right) >= 0f ? 1f : -1f);

        return contact.Velocity;
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
    Vector3 ResolveAttitude(bool hasContact, Vector3 groundNormal, float spinYawRate)
    {
        Vector3 targetUp = hasContact ? groundNormal : Vector3.up;
        float strength = hasContact ? handling.GroundedLevelStrength : handling.AirLevelStrength;

        // Cross product gives the axis to rotate about, scaled by the sine of the error - so it
        // falls to zero as the chassis comes level, with no overshoot to damp.
        Vector3 levelling = Vector3.Cross(transform.up, targetUp) * strength;

        return levelling + transform.up * ((YawRate + spinYawRate) * Mathf.Deg2Rad);
    }

    Vector3 ResolveGrounded(PlayerNetInput input, Vector3 velocity, float dt)
    {
        Vector3 local = transform.InverseTransformDirection(velocity);

        // Step 5: suspension. While grounded, gravity is carried by the suspension: a spring and
        // damper per grounded probe hold each wheel centre at exactly wheelRadius above the
        // ground - the height where the wheel colliders just touch - resolved as a vertical
        // acceleration rather than AddForceAtPosition (the model owns its own integration).
        //
        // Preloaded on purpose, so the ride height doesn't depend on SpringStrength. An earlier
        // version measured compression from the touching height and added gravity on top, so
        // the spring was zero AT rest height and the chassis sank until spring balanced
        // gravity - 39% down, wheel spheres 12cm into the floor. PhysX depenetrated them every
        // step, the model read that push back as velocity, and the two "support systems" fought:
        // the car crept on slopes and edges by itself, and four deep contacts resisted a spin in
        // place. Doc 03 treats chassis lean as visual weight, so the asymmetry between corners
        // drives the cosmetic tilt in LateUpdate instead of real body rotation.
        float suspension = GroundProbes.SuspensionResponse(probes, handling.SpringStrength, handling.DamperStrength);
        float vertical = local.y + suspension * dt;

        // Step 6: longitudinal.
        float forwardSpeed = ResolveLongitudinal(input, local.z, dt);
        ForwardSpeed = forwardSpeed;

        // Step 7: steering.
        ResolveSteering(input, forwardSpeed, dt, authority: 1f);

        // Step 8: lateral. Sideways velocity decays exponentially at a per-second rate - the gap
        // between where the chassis points and where it's going is the slide, and every drift
        // is that gap being opened deliberately. Scrub is not returned to the chassis.
        //
        // A rate, not a fraction-per-tick: an earlier version removed 86% of sideways velocity
        // EVERY tick (60 times a second), which is a rail - the car snapped to its heading in
        // two ticks and even the drift button barely slid. exp(-rate*dt) gives the same feel at
        // any tick rate, which also matters for the still-open tick rate ruling (N2).
        float gripRate = input.Drift ? handling.DriftGripRate : handling.GripRate;
        float lateral = local.x * Mathf.Exp(-gripRate * dt);
        if (Mathf.Abs(lateral) < RestDeadband) lateral = 0f; // exponential decay never reaches zero on its own

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
    //
    // Yaw authority comes from speed (SpeedScalingCurve is zero at a standstill): a parked car
    // doesn't pivot in place, and while reversing the nose swings the other way - both as a
    // real car steers, by request.
    void ResolveSteering(PlayerNetInput input, float forwardSpeed, float dt, float authority)
    {
        float speedRatio = handling.TopSpeed > 0f ? Mathf.Abs(forwardSpeed) / handling.TopSpeed : 0f;
        float speedScale = handling.SpeedScalingCurve?.Evaluate(Mathf.Clamp01(speedRatio)) ?? 1f;
        float travelDirection = forwardSpeed < 0f ? -1f : 1f;

        const float driftRotationAuthority = 1f; // neutral until Drift & Boost (doc 04) exists

        float targetYawRate = input.SteerAxis * travelDirection * handling.MaxYawRate * speedScale * driftRotationAuthority * authority;

        float responseTime = Mathf.Max(0.001f, handling.SteerResponse * handling.Inertia);
        float alpha = 1f - Mathf.Exp(-dt / responseTime);
        YawRate += (targetYawRate - YawRate) * alpha;
    }

    // Steps 5 through 8 are replaced entirely while airborne. Steering authority is reduced but
    // never zero - no state in this foundation leaves the player unable to steer.
    // Resolved in WORLD space, unlike the grounded step: a chassis mid-air may be tilted, and
    // its own axes mean nothing to gravity or to "horizontal".
    Vector3 ResolveAirborne(PlayerNetInput input, Vector3 velocity, float dt)
    {
        float forwardSpeed = Vector3.Dot(velocity, transform.forward);
        ResolveSteering(input, forwardSpeed, dt, authority: handling.AirSteerMultiplier);
        ForwardSpeed = forwardSpeed;

        // Air drag is gentler than coast drag - a jump shouldn't cost the player their speed.
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float horizontalSpeed = Mathf.MoveTowards(horizontal.magnitude, 0f, handling.AirDrag * dt);
        horizontal = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized * horizontalSpeed : Vector3.zero;

        // Gravity is scaled above real gravity for arcade weight and a fast return to ground.
        float vertical = velocity.y + Physics.gravity.y * handling.GravityScale * dt;

        return new Vector3(horizontal.x, vertical, horizontal.z);
    }

    // Purely cosmetic, and deliberately so - doc 03 counts chassis lean as "visual weight, at no
    // gameplay cost". Tires roll from real forward speed and steer from the replicated input;
    // the body tilts from the difference in suspension compression across the probes, which is
    // now real per-corner data rather than an approximation.
    void LateUpdate()
    {
        if (!matchState.HasState) return; // SteerInput is networked; see PlayerMatchState.HasState

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
