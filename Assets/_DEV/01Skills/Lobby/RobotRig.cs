using UnityEngine;

// Describes one lobby robot's wheels, so the car's cosmetic wheel animation (PlayerMovement) and tyre
// tracks (CarSkidmarks) follow whichever robot is active. Robots don't share a layout (Spark has
// 3 wheels, Gravy 2 + 1 back): leave a slot empty if the robot has no such wheel.
//
// VehicleVisuals overwrites each tire's localRotation (roll + steer), so assign a transform whose
// rest rotation is zero - if a tire mesh is rotated, wrap it in an empty pivot and assign the pivot.
// Put it on each robot child of the car's "Bots" object.
public class RobotRig : MonoBehaviour
{
    [Header("Wheels animated by PlayerMovement (empty = none)")]
    public Transform frontLeft;
    public Transform frontRight;
    public Transform rearLeft;
    public Transform rearRight;

    [Header("Wheels that leave skidmarks (usually the rear ones)")]
    public Transform[] skidmarkWheels;
}
