using UnityEngine;

public static class DriftBoost
{
    public static DriftBoostEvents Tick(ref DriftBoostState s, in DriftBoostInput i, HandlingValues h)
    {
        var ev = default(DriftBoostEvents);

        bool hopPressed = i.Hop && !s.HopHeldLastTick;
        s.HopHeldLastTick = i.Hop;
        float speed = Mathf.Abs(i.ForwardSpeed);

        TickBoostTimers(ref s, h);

        switch (s.Phase)
        {
            case DriftPhase.Neutral:
                {
                    if (hopPressed && i.Grounded && speed >= h.DriftMinSpeed)
                    {
                        s.Phase = DriftPhase.Hop;
                        s.PhaseTicks = 0;
                        ev.HopStarted = true;
                    }
                    break;
                }

            case DriftPhase.Hop:
                {
                    s.PhaseTicks++;
                    if (s.PhaseTicks < h.HopTicks || !i.Grounded) break; // still in the air / hop timer running

                    // Landing: the steer direction held right now commits the drift. No steer (or the
                    // button let go) = back to Neutral with no penalty.
                    bool commit = i.Hop
                                  && Mathf.Abs(i.Steer) >= h.SteerCommitDeadzone
                                  && speed >= h.DriftMinSpeed;
                    if (commit)
                    {
                        s.Phase = DriftPhase.Sliding;
                        s.Direction = i.Steer > 0f ? 1 : -1;
                        s.Reserve = 0f;
                        s.PhaseTicks = 0;
                        ev.DriftStarted = true;
                    }
                    else
                    {
                        ReturnToNeutral(ref s);
                    }
                    break;
                }

            case DriftPhase.Sliding:
                {
                    bool released = !i.Hop || speed < h.DriftMinSpeed;
                    if (released || !i.Grounded || i.Collided)
                    {
                        // Not enough for stage 1: nothing to convert, no window.
                        if (s.Reserve < h.Stage1Threshold)
                        {
                            ReturnToNeutral(ref s);
                            break;
                        }

                        s.Phase = DriftPhase.Window;
                        s.PhaseTicks = 0;
                        ev.WindowOpened = true;
                        goto case DriftPhase.Window; // a held boost button counts on this very tick
                    }

                    // Charge: slip angle x speed x surface, only while Sliding and grounded.
                    float slipDeg = Mathf.Atan2(Mathf.Abs(i.LateralSpeed), Mathf.Max(0.01f, speed)) * Mathf.Rad2Deg;
                    float slipFactor = SlipFactor(slipDeg, h.OptimalSlipAngle, h.MaxSlipAngle);
                    float speedFactor = Mathf.Lerp(h.SpeedChargeFloor, 1f, Mathf.Clamp01(speed / Mathf.Max(0.01f, h.TopSpeed)));
                    float gain = h.ReserveGainRate * slipFactor * speedFactor * Mathf.Max(0f, i.SurfaceChargeFactor);

                    s.Reserve = Mathf.Min(h.Stage3Threshold, s.Reserve + gain);
                    break;
                }

            case DriftPhase.Window:
                {
                    // Hopping again without boosting: the window closes and the reserve is lost.
                    if (hopPressed)
                    {
                        s.Reserve = 0f;
                        ev.Fizzled = true;
                        ReturnToNeutral(ref s);
                        goto case DriftPhase.Neutral;
                    }

                    if (!i.Grounded) break; // window opens on landing; the timer doesn't run in the air

                    if (i.Boost)
                    {
                        StartBoost(ref s, StageFor(s.Reserve, h), i.ForwardSpeed, h, i.Tick);
                        ev.BoostStarted = true;
                        ReturnToNeutral(ref s); // reserve converted
                        break;
                    }

                    s.PhaseTicks++;
                    if (s.PhaseTicks >= h.WindowTicks)
                    {
                        ev.Fizzled = true;
                        ReturnToNeutral(ref s); // reserve lost
                    }
                    // Reset the chain only after this tick's input had a chance
                    // to start another boost during the final grace tick.
                    if (s.BoostTicksRemaining == 0 &&
                        s.ChainGraceTicksRemaining == 0)
                    {
                        s.ChainLinks = 0;
                    }
                    break;
                }
        }

        return ev;
    }

    // ─── Boost ──────────────────────────────────────────────────────────

    // Starts (or chains) a boost of the given stage 1..3. Drift release and Overdrive both come through
    // here, so there is one boost economy. Overdrive: StartBoost(ref s, 2, speed, h, tick).
    public static void StartBoost(ref DriftBoostState s, int stage, float forwardSpeed, HandlingValues h, int tick)
    {
        float multiplier = BoostMultiplierFor(stage, h);

        // Two boosts on the same tick = one chain link at the higher multiplier, never additive.
        if (s.IsBoosting && s.LastBoostStartTick == tick)
        {
            if (multiplier > s.BoostMultiplier) s.BoostMultiplier = multiplier;
            s.BoostTicksRemaining = Mathf.Max(s.BoostTicksRemaining, h.BoostTicks);
            return;
        }

        bool chained = s.IsBoosting || s.ChainGraceTicksRemaining > 0 || s.ChainLinks > 0;
        s.ChainLinks = chained ? s.ChainLinks + 1 : 1;

        // Each chain link lifts the target from CURRENT speed, which is what carries the car
        // above nominal top speed. No clamp, no cap on chain length.
        s.BoostBaseSpeed = chained ? Mathf.Max(h.TopSpeed, forwardSpeed) : h.TopSpeed;
        s.BoostMultiplier = multiplier;
        s.BoostTicksRemaining = h.BoostTicks;
        s.ChainGraceTicksRemaining = 0;
        s.LastBoostStartTick = tick;
    }

    static void TickBoostTimers(ref DriftBoostState s, HandlingValues h)
    {
        if (s.BoostTicksRemaining > 0)
        {
            s.BoostTicksRemaining--;

            if (s.BoostTicksRemaining == 0)
            {
                s.ChainGraceTicksRemaining = h.ChainGraceTicks;
            }
        }
        else if (s.ChainGraceTicksRemaining > 0)
        {
            s.ChainGraceTicksRemaining--;
        }

        // Keep the chain alive until StartBoost has had a chance to
        // process this tick's input. ChainLinks resets on the next tick
        // if the grace period expired and no new boost was started.
    }

    // Respawn clears both reserve and chain.
    public static void Reset(ref DriftBoostState s)
    {
        s = default;
    }

    // ─── What the drive model reads ─────────────────────────────────────

    // Target forward speed while boosting (no clamp at TopSpeed; OverspeedDecay brings it back down).
    // Only meaningful while s.IsBoosting.
    public static float BoostTargetSpeed(in DriftBoostState s) => s.BoostBaseSpeed * s.BoostMultiplier;

    // Lateral grip multiplier, applied automatically while Sliding (drift_grip_multiplier). Not a button.
    public static float GripMultiplier(in DriftBoostState s, HandlingValues h) =>
        s.IsSliding ? h.DriftGripMultiplier : 1f;

    // Multiplier on target yaw rate while Sliding (drift_rotation_authority).
    public static float YawAuthority(in DriftBoostState s, HandlingValues h) =>
        s.IsSliding ? h.DriftRotationAuthority : 1f;

    // boost_bump_multiplier: only while the aggressor is Boosting or Chained (chain grace included).
    // Multiplies the arcade bonus component of the contact, not the whole impulse (contact code owns that).
    public static float BumpMultiplier(in DriftBoostState s, HandlingValues h)
    {
        return (s.IsBoosting || s.IsChained || s.IsInChainGrace)
            ? h.BoostBumpMultiplier
            : 1f;
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    // Stage reached by the current reserve (0 = none). While Sliding, stage >= 1 is the "Charged" sub-state.
    public static int CurrentStage(in DriftBoostState s, HandlingValues h) => StageFor(s.Reserve, h);

    public static int StageFor(float reserve, HandlingValues h)
    {
        if (reserve >= h.Stage3Threshold) return 3;
        if (reserve >= h.Stage2Threshold) return 2;
        if (reserve >= h.Stage1Threshold) return 1;
        return 0;
    }

    static float BoostMultiplierFor(int stage, HandlingValues h)
    {
        switch (stage)
        {
            case 3: return h.Stage3BoostMultiplier;
            case 2: return h.Stage2BoostMultiplier;
            default: return h.Stage1BoostMultiplier;
        }
    }

    // 1.0 at the optimal slip angle: rises up to it, falls off past it (a sideways drift charges slowly).
    static float SlipFactor(float angleDeg, float optimalDeg, float maxDeg)
    {
        if (angleDeg <= optimalDeg) return optimalDeg <= 0f ? 0f : angleDeg / optimalDeg;
        float t = (angleDeg - optimalDeg) / Mathf.Max(0.01f, maxDeg - optimalDeg);
        return Mathf.Lerp(1f, 0.1f, Mathf.Clamp01(t));
    }

    static void ReturnToNeutral(ref DriftBoostState s)
    {
        s.Phase = DriftPhase.Neutral;
        s.PhaseTicks = 0;
        s.Reserve = 0f;
    }
}