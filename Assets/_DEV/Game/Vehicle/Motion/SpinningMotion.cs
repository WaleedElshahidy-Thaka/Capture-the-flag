using UnityEngine;

// During a spin-out the chassis slides like a puck: it keeps its direction of travel, bleeds
// speed at coast drag, and reduced throttle still pushes along wherever the nose points.
public class SpinningMotion : IMotionMode
{
    readonly HandlingValues handling;
    readonly SteeringModel steering;

    public SpinningMotion(HandlingValues handling, SteeringModel steering)
    {
        this.handling = handling;
        this.steering = steering;
    }

    public Vector3 Resolve(PlayerNetInput input, Vector3 velocity, in MotionContext context,
                           ref DriveState state, out float forwardSpeed)
    {
        float dt = context.DeltaTime;
        Vector3 forward = context.Chassis.forward;

        Vector3 horizontal = MotionMath.DragHorizontal(velocity, handling.CoastDrag, dt);
        Vector3 nose = new Vector3(forward.x, 0f, forward.z).normalized;
        horizontal += nose * (input.ThrottleAxis * handling.Acceleration * dt);
        if (horizontal.magnitude > handling.TopSpeed) horizontal = horizontal.normalized * handling.TopSpeed;

        forwardSpeed = Vector3.Dot(horizontal, nose);
        state.YawRate = steering.NextYawRate(state.YawRate, input.SteerAxis, forwardSpeed, 1f, dt);

        float vertical = context.IsGrounded
            ? MotionMath.Suspended(velocity.y, context.Probes, handling, dt)
            : MotionMath.FreeFall(velocity.y, handling, dt);

        return new Vector3(horizontal.x, vertical, horizontal.z);
    }
}
