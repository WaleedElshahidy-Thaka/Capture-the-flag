using UnityEngine;

// In the air: gentle drag, scaled gravity and reduced (never zero) steering.
// Resolved in world space because a tilted chassis's own axes mean nothing to gravity.
public class AirborneMotion : IMotionMode
{
    readonly HandlingValues handling;
    readonly SteeringModel steering;

    public AirborneMotion(HandlingValues handling, SteeringModel steering)
    {
        this.handling = handling;
        this.steering = steering;
    }

    public Vector3 Resolve(PlayerNetInput input, Vector3 velocity, in MotionContext context,
                           ref DriveState state, out float forwardSpeed)
    {
        float dt = context.DeltaTime;

        forwardSpeed = Vector3.Dot(velocity, context.Chassis.forward);
        state.YawRate = steering.NextYawRate(state.YawRate, input.SteerAxis, forwardSpeed, handling.AirSteerMultiplier, dt);

        Vector3 horizontal = MotionMath.DragHorizontal(velocity, handling.AirDrag, dt);
        float vertical = MotionMath.FreeFall(velocity.y, handling, dt);

        return new Vector3(horizontal.x, vertical, horizontal.z);
    }
}
