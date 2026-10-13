using UnityEngine;

// One way the chassis can move (grounded, airborne, spinning out). VehicleDriveModel picks one
// per tick; adding a new mode (e.g. drift) means adding a class, not editing the others.
public interface IMotionMode
{
    // Returns the velocity to write this tick and the chassis's forward speed.
    // May update the steering/brake fields of state.
    Vector3 Resolve(PlayerNetInput input, Vector3 velocity, in MotionContext context,
                    ref DriveState state, out float forwardSpeed);
}
