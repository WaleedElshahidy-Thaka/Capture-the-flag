using UnityEngine;

// One tick of the vehicle drive model (GDD doc 03), independent of networking:
//   probe ground -> tick recovery -> pick motion mode -> resolve walls -> write velocities.
// Velocity is composed and written each tick rather than accumulated through forces, so a
// re-simulated tick from restored state lands on the same result (rollback-safe).
public class VehicleDriveModel
{
    const int MinGroundedProbes = 2;

    readonly Transform chassis;
    readonly Rigidbody body;
    readonly HandlingValues handling;
    readonly Vector3[] probeOffsets;
    readonly float wheelRadius;
    readonly LayerMask groundMask;

    readonly SpinoutRecovery spinout;
    readonly WallSensor wallSensor;
    readonly IMotionMode grounded;
    readonly IMotionMode airborne;
    readonly IMotionMode spinning;

    readonly ProbeResult[] probes = new ProbeResult[4];
    bool touchingWall; // only so a scrape logs once, not every tick

    public bool IsGrounded { get; private set; }
    public float ForwardSpeed { get; private set; }
    public ProbeResult[] Probes => probes;

    // True if the last simulated tick ended in wall contact. Layer C reads it so a wall hit opens the
    // drift's release window (contact never costs charge).
    public bool TouchingWall => touchingWall;

    public VehicleDriveModel(Rigidbody body, HandlingValues handling, Vector3[] probeOffsets,
                             float wheelRadius, LayerMask groundMask)
    {
        this.body = body;
        chassis = body.transform;
        this.handling = handling;
        this.probeOffsets = probeOffsets;
        this.wheelRadius = wheelRadius;
        this.groundMask = groundMask;

        var steering = new SteeringModel(handling);
        spinout = new SpinoutRecovery(handling);
        wallSensor = new WallSensor(groundMask);
        grounded = new GroundedMotion(handling, new LongitudinalDrive(handling), steering);
        airborne = new AirborneMotion(handling, steering);
        spinning = new SpinningMotion(handling, steering);
    }

    // Simulates one tick and writes the result to the Rigidbody.
    // Returns the steer axis actually applied (after spin-out scaling), for the wheel visuals.
    public float Simulate(PlayerNetInput input, ref DriveState state, float dt)
    {
        GroundProbes.Cast(chassis, body, probeOffsets, wheelRadius + handling.SuspensionTravel,
                          wheelRadius, groundMask, probes);

        Vector3 groundNormal = GroundProbes.AverageGroundedNormal(probes, Vector3.up);
        bool hasContact = GroundProbes.CountGrounded(probes) >= MinGroundedProbes;
        // Must also be the right way up: a car on its side shouldn't drive along the floor.
        IsGrounded = hasContact && Vector3.Angle(chassis.up, groundNormal) <= handling.MaxDriveAngle;

        // Read back from the body so wall/car pushes from PhysX since last tick are kept.
        Vector3 velocity = body.linearVelocity;
        float currentYaw = chassis.eulerAngles.y;

        float spinYawRate = spinout.Tick(ref state, ref input, currentYaw, dt);

        var context = new MotionContext(chassis, probes, IsGrounded, dt);
        velocity = CurrentMode(state).Resolve(input, velocity, context, ref state, out float forwardSpeed);
        ForwardSpeed = forwardSpeed;

        velocity = ResolveWalls(velocity, ref state, currentYaw, dt);

        body.linearVelocity = velocity;
        body.angularVelocity = ChassisAttitude.AngularVelocity(chassis, hasContact, groundNormal,
                                                               state.YawRate + spinYawRate, handling);
        return input.SteerAxis;
    }

    // Starts a spin-out outside the normal tick (e.g. a slam from another car).
    public void EnterSpinout(ref DriveState state, Vector3 travelDirection, float spinSign) =>
        spinout.TryEnter(ref state, travelDirection, spinSign, chassis.forward, chassis.eulerAngles.y);

    IMotionMode CurrentMode(DriveState state)
    {
        if (state.IsSpinningOut) return spinning;
        return IsGrounded ? grounded : airborne;
    }

    // Replaces the composed velocity before PhysX can bounce the chassis off a wall.
    Vector3 ResolveWalls(Vector3 velocity, ref DriveState state, float currentYaw, float dt)
    {
        Vector3 heading = new Vector3(velocity.x, 0f, velocity.z);
        float speed = heading.magnitude;
        if (speed < 0.01f) { touchingWall = false; return velocity; }
        heading /= speed;

        Vector3 origin = chassis.position + Vector3.up * wheelRadius;
        if (!wallSensor.TryFindWall(origin, heading, speed, dt, out var wall)
            || !WallContact.TryResolve(velocity, wall.normal, WallContact.SideHint(chassis.forward, wall.normal), handling, out var contact))
        {
            touchingWall = false;
            return velocity;
        }

        if (!touchingWall)
            Debug.Log($"[WallContact] {contact.Class} at {contact.IncidenceDegrees:0}°, {contact.ClosingSpeed:0.0} m/s in -> {contact.Velocity.magnitude:0.0} m/s out{(contact.QualifiesForSpinout ? " (spin-out speed)" : "")}");
        touchingWall = true;

        if (contact.QualifiesForSpinout)
        {
            float spinSign = Vector3.Dot(contact.Velocity, chassis.right) >= 0f ? 1f : -1f;
            spinout.TryEnter(ref state, contact.Velocity, spinSign, chassis.forward, currentYaw);
        }

        return contact.Velocity;
    }
}