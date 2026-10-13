using UnityEngine;

// What a motion mode needs to know about the chassis for one tick.
public readonly struct MotionContext
{
    public readonly Transform Chassis;
    public readonly ProbeResult[] Probes;
    public readonly bool IsGrounded;
    public readonly float DeltaTime;

    public MotionContext(Transform chassis, ProbeResult[] probes, bool isGrounded, float deltaTime)
    {
        Chassis = chassis;
        Probes = probes;
        IsGrounded = isGrounded;
        DeltaTime = deltaTime;
    }
}
