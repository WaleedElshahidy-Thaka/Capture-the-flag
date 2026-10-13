using UnityEngine;

// Small vector helpers shared by the motion modes.
public static class MotionMath
{
    // Horizontal part of the velocity, slowed toward zero by `drag` m/s².
    public static Vector3 DragHorizontal(Vector3 velocity, float drag, float dt)
    {
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float speed = Mathf.MoveTowards(horizontal.magnitude, 0f, drag * dt);
        return horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized * speed : Vector3.zero;
    }

    // Vertical speed after one tick of (scaled) gravity.
    public static float FreeFall(float verticalSpeed, HandlingValues handling, float dt) =>
        verticalSpeed + Physics.gravity.y * handling.GravityScale * dt;

    // Vertical speed after one tick of suspension support.
    public static float Suspended(float verticalSpeed, ProbeResult[] probes, HandlingValues handling, float dt) =>
        verticalSpeed + GroundProbes.SuspensionResponse(probes, handling.SpringStrength, handling.DamperStrength) * dt;
}
