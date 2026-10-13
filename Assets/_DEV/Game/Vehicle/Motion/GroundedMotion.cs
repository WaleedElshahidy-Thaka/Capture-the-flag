using UnityEngine;

// On the ground: suspension holds ride height, the drive sets forward speed, steering sets yaw
// and grip scrubs off sideways slide. Resolved in the chassis's local space.
public class GroundedMotion : IMotionMode
{
    const float RestDeadband = 0.01f; // m/s; exponential grip decay never reaches zero by itself

    readonly HandlingValues handling;
    readonly LongitudinalDrive drive;
    readonly SteeringModel steering;

    public GroundedMotion(HandlingValues handling, LongitudinalDrive drive, SteeringModel steering)
    {
        this.handling = handling;
        this.drive = drive;
        this.steering = steering;
    }

    public Vector3 Resolve(PlayerNetInput input, Vector3 velocity, in MotionContext context,
                           ref DriveState state, out float forwardSpeed)
    {
        float dt = context.DeltaTime;
        Vector3 local = context.Chassis.InverseTransformDirection(velocity);

        float vertical = MotionMath.Suspended(local.y, context.Probes, handling, dt);

        forwardSpeed = drive.NextForwardSpeed(input, local.z, ref state.BrakeHoldSeconds, dt);
        state.YawRate = steering.NextYawRate(state.YawRate, input.SteerAxis, forwardSpeed, 1f, dt);

        // Sideways speed decays per second (not per tick), so the feel is tick-rate independent.
        float gripRate = input.Drift ? handling.DriftGripRate : handling.GripRate;
        float lateral = local.x * Mathf.Exp(-gripRate * dt);
        if (Mathf.Abs(lateral) < RestDeadband) lateral = 0f;

        return context.Chassis.TransformDirection(new Vector3(lateral, vertical, forwardSpeed));
    }
}
