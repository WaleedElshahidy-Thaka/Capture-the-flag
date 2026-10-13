using UnityEngine;

// Purely cosmetic: rolls and steers the tire meshes and leans the body from per-corner
// suspension compression. Never touches the Rigidbody, so it has no gameplay effect.
public class VehicleVisuals
{
    public struct Rig
    {
        public Transform Body;        // parent of the tires; rotated for lean
        public Transform FrontLeft, FrontRight, RearLeft, RearRight;
        public float WheelRadius;
        public float MaxTireSteerAngle;
        public float MaxLeanAngle;
        public float LeanSpring;
    }

    readonly Rig rig;
    float tireRollAngle;
    Quaternion leanRotation = Quaternion.identity;

    public VehicleVisuals(Rig rig) => this.rig = rig;

    public void Animate(float forwardSpeed, float steerInput, ProbeResult[] probes, float dt)
    {
        tireRollAngle += (forwardSpeed / rig.WheelRadius) * Mathf.Rad2Deg * dt;
        tireRollAngle %= 360f;

        float steerAngle = steerInput * rig.MaxTireSteerAngle;
        SetTire(rig.FrontLeft, steerAngle);
        SetTire(rig.FrontRight, steerAngle);
        SetTire(rig.RearLeft, 0f);
        SetTire(rig.RearRight, 0f);

        if (rig.Body != null) Lean(probes, dt);
    }

    // Probe order: front-left, front-right, rear-left, rear-right.
    void Lean(ProbeResult[] probes, float dt)
    {
        float left = (probes[0].Compression + probes[2].Compression) * 0.5f;
        float right = (probes[1].Compression + probes[3].Compression) * 0.5f;
        float front = (probes[0].Compression + probes[1].Compression) * 0.5f;
        float rear = (probes[2].Compression + probes[3].Compression) * 0.5f;

        Quaternion target = Quaternion.Euler((rear - front) * rig.MaxLeanAngle, 0f, -(right - left) * rig.MaxLeanAngle);
        float alpha = 1f - Mathf.Exp(-rig.LeanSpring * dt);
        leanRotation = Quaternion.Slerp(leanRotation, target, alpha);
        rig.Body.localRotation = leanRotation;
    }

    void SetTire(Transform tire, float steerAngle)
    {
        if (tire == null) return;
        tire.localRotation = Quaternion.Euler(tireRollAngle, steerAngle, 0f);
    }
}
