using UnityEngine;

// The spin-out recovery state (GDD doc 05). A hard hit spins the chassis round on an eased
// curve so it lands facing its direction of travel, then grants a short immunity window.
// Input is reduced, never removed, while spinning.
public class SpinoutRecovery
{
    readonly HandlingValues handling;

    public SpinoutRecovery(HandlingValues handling) => this.handling = handling;

    int TotalTicks => Mathf.Max(1, handling.SpinoutTicks);

    // Refused while already spinning or immune. spinSign: +1 clockwise, -1 counter-clockwise.
    public void TryEnter(ref DriveState state, Vector3 travelDirection, float spinSign, Vector3 chassisForward, float currentYaw)
    {
        if (state.IsSpinningOut || state.HasContactImmunity) return;

        Vector3 travel = new Vector3(travelDirection.x, 0f, travelDirection.z);
        if (travel.sqrMagnitude < 0.0001f) travel = chassisForward;
        float settle = Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg;

        state.SpinoutTicksRemaining = TotalTicks;
        state.SettleHeading = settle;
        state.SpinDegrees = SpinAngle(currentYaw, settle, spinSign);
        state.YawRate = 0f;

        Debug.Log($"[Spinout] enter: {state.SpinDegrees:0}° over {state.SpinoutTicksRemaining} ticks, settling at {settle:0}°");
    }

    // Advances one tick. Scales the input while spinning and returns the extra yaw rate (deg/s)
    // the spin needs this tick; 0 when not spinning.
    public float Tick(ref DriveState state, ref PlayerNetInput input, float currentYaw, float dt)
    {
        if (state.ImmunityTicksRemaining > 0) state.ImmunityTicksRemaining--;
        if (!state.IsSpinningOut) return 0f;

        input.SteerAxis *= handling.SpinoutSteerMultiplier;
        input.ThrottleAxis *= handling.SpinoutThrottleMultiplier;
        input.Drift = false;

        float progress = 1f - (state.SpinoutTicksRemaining - 1) / (float)TotalTicks;
        float eased = progress * progress * (3f - 2f * progress); // smoothstep
        float desiredYaw = state.SettleHeading - (1f - eased) * state.SpinDegrees;
        float yawRate = Mathf.DeltaAngle(currentYaw, desiredYaw) / dt;

        state.SpinoutTicksRemaining--;
        if (state.SpinoutTicksRemaining == 0)
        {
            state.ImmunityTicksRemaining = handling.ContactImmunityTicks;
            Debug.Log($"[Spinout] exit: facing {currentYaw:0}°, immune for {state.ImmunityTicksRemaining} ticks");
        }

        return yawRate;
    }

    // One full turn in the spin direction plus whatever lands on the settle heading, kept between
    // half a turn and one and a half so it always reads as a spin, never a twitch.
    static float SpinAngle(float currentYaw, float settleYaw, float spinSign)
    {
        float sign = spinSign >= 0f ? 1f : -1f;
        float toSettle = Mathf.DeltaAngle(currentYaw, settleYaw);
        if (sign > 0f && toSettle < 0f) toSettle += 360f;
        if (sign < 0f && toSettle > 0f) toSettle -= 360f;

        float total = toSettle + sign * 360f;
        if (Mathf.Abs(total) > 540f) total -= sign * 360f;
        return total;
    }
}
