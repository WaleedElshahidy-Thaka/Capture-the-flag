using UnityEngine;

// What a motion mode needs to know about the chassis for one tick.
public readonly struct MotionContext
{
    public readonly Transform Chassis;
    public readonly ProbeResult[] Probes;
    public readonly bool IsGrounded;
    public readonly float DeltaTime;

    // What the surface under the probe majority does this tick. 1 = Default surface.
    public readonly float SurfaceGripMultiplier;
    public readonly float SurfaceSteerAuthority;

    public MotionContext(Transform chassis, ProbeResult[] probes, bool isGrounded, float deltaTime,
                         float surfaceGripMultiplier = 1f, float surfaceSteerAuthority = 1f)
    {
        Chassis = chassis;
        Probes = probes;
        IsGrounded = isGrounded;
        DeltaTime = deltaTime;
        SurfaceGripMultiplier = surfaceGripMultiplier;
        SurfaceSteerAuthority = surfaceSteerAuthority;
    }
}
