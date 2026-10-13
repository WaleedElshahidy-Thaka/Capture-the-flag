using UnityEngine;

// Forward/backward speed: throttle, brake, reverse (after a short hold at rest), coasting,
// and the gentle decay that lets boost carry the car above its nominal top speed.
public class LongitudinalDrive
{
    const float InputThreshold = 0.01f;
    const float StillMovingForward = 0.1f; // m/s

    readonly HandlingValues handling;

    public LongitudinalDrive(HandlingValues handling) => this.handling = handling;

    public float NextForwardSpeed(PlayerNetInput input, float forwardSpeed, ref float brakeHoldSeconds, float dt)
    {
        bool throttle = input.ThrottleAxis > InputThreshold;
        bool brake = input.ThrottleAxis < -InputThreshold;

        float target;
        float rate;

        if (throttle && !brake)
        {
            target = handling.TopSpeed * (input.Boost ? handling.BoostMultiplier : 1f);
            rate = handling.Acceleration;
            brakeHoldSeconds = 0f;
        }
        else if (brake && forwardSpeed > StillMovingForward)
        {
            target = 0f;
            rate = handling.BrakeStrength;
            brakeHoldSeconds = 0f;
        }
        else if (brake)
        {
            // At rest: reverse engages only after ReverseDelay, so a tap doesn't reverse by accident.
            brakeHoldSeconds += dt;
            bool reverseEngaged = brakeHoldSeconds >= handling.ReverseDelay;
            target = reverseEngaged ? -handling.ReverseTopSpeed : 0f;
            rate = reverseEngaged ? handling.Acceleration : handling.BrakeStrength;
        }
        else
        {
            target = 0f;
            rate = handling.CoastDrag;
            brakeHoldSeconds = 0f;
        }

        // Above top speed (after a boost) speed bleeds off slowly so boosts can chain.
        // Braking is exempt: the player can always stop.
        bool coastingAboveCeiling = !brake
                                    && Mathf.Abs(forwardSpeed) > handling.TopSpeed
                                    && Mathf.Abs(forwardSpeed) > Mathf.Abs(target);
        if (coastingAboveCeiling) rate = handling.OverspeedDecay;

        return Mathf.MoveTowards(forwardSpeed, target, rate * dt);
    }
}
