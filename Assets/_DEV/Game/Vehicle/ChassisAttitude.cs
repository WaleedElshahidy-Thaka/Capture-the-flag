using UnityEngine;

// The angular velocity to write each tick: pitch/roll levelled toward the ground normal
// (or world up in the air, so the car always lands on its wheels), plus yaw from steering.
public static class ChassisAttitude
{
    public static Vector3 AngularVelocity(Transform chassis, bool hasContact, Vector3 groundNormal,
                                          float yawRateDegrees, HandlingValues handling)
    {
        Vector3 targetUp = hasContact ? groundNormal : Vector3.up;
        float strength = hasContact ? handling.GroundedLevelStrength : handling.AirLevelStrength;

        // Scaled by the sine of the error, so it fades to zero as the chassis comes level.
        Vector3 levelling = Vector3.Cross(chassis.up, targetUp) * strength;

        return levelling + chassis.up * (yawRateDegrees * Mathf.Deg2Rad);
    }
}
