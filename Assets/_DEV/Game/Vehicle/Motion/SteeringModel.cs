using UnityEngine;

// Steer input sets a target yaw rate; the chassis eases toward it at a speed set by
// SteerResponse × Inertia. Authority scales with speed (no pivoting at a standstill) and
// flips while reversing, like a real car.
public class SteeringModel
{
    const float DriftRotationAuthority = 1f; // neutral until the drift system exists

    readonly HandlingValues handling;

    public SteeringModel(HandlingValues handling) => this.handling = handling;

    public float NextYawRate(float currentYawRate, float steerAxis, float forwardSpeed, float authority, float dt)
    {
        float speedRatio = handling.TopSpeed > 0f ? Mathf.Abs(forwardSpeed) / handling.TopSpeed : 0f;
        float speedScale = handling.SpeedScalingCurve?.Evaluate(Mathf.Clamp01(speedRatio)) ?? 1f;
        float travelDirection = forwardSpeed < 0f ? -1f : 1f;

        float targetYawRate = steerAxis * travelDirection * handling.MaxYawRate * speedScale * DriftRotationAuthority * authority;

        float responseTime = Mathf.Max(0.001f, handling.SteerResponse * handling.Inertia);
        float alpha = 1f - Mathf.Exp(-dt / responseTime);
        return currentYawRate + (targetYawRate - currentYawRate) * alpha;
    }
}
