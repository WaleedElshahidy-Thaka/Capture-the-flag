using UnityEngine;

// Layer C: Hold-to-Boost Model v0.1 + Direction Lock & Control Split v0.1. See DriftPhase.cs.
public static class DriftBoost
{
    public static DriftBoostEvents Tick(ref DriftBoostState s, in DriftBoostInput i, HandlingValues h)
    {
        var ev = default(DriftBoostEvents);

        bool hopPressed = i.Hop && !s.HopHeldLastTick;
        s.HopHeldLastTick = i.Hop;
        bool boostPressed = i.Boost && !s.BoostHeldLastTick; // a held Shift never auto-fires the next stage
        s.BoostHeldLastTick = i.Boost;
        float speed = Mathf.Abs(i.ForwardSpeed);

        TickBoostTimers(ref s, h);

        switch (s.Phase)
        {
            case DriftPhase.Neutral:
                {
                    // A press, not a hold: after a speed dropout, holding Space doesn't restart the slide.
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

                    // Landing: the steer held right now locks the slide's direction. No steer (or Space let
                    // go) = it was just a hop, back to Neutral with no penalty.
                    bool commit = i.Hop
                                  && Mathf.Abs(i.Steer) >= h.SteerCommitDeadzone
                                  && speed >= h.DriftMinSpeed;
                    if (commit)
                    {
                        s.Phase = DriftPhase.Sliding;
                        s.Direction = i.Steer > 0f ? 1 : -1;
                        s.PhaseTicks = 0;
                        s.NotSlidingTicks = 0;
                        ev.DriftStarted = true;
                        // Reserve and StageReached carry on from the last slide (within StageResetTicks).
                    }
                    else
                    {
                        s.Phase = DriftPhase.Neutral;
                        s.PhaseTicks = 0;
                    }
                    break;
                }

            case DriftPhase.Sliding:
                {
                    // Space released or too slow: the slide ends. Space is the only deliberate way out.
                    if (!i.Hop || speed < h.DriftMinSpeed)
                    {
                        EndSlide(ref s, ref ev);
                        break;
                    }

                    // Left the ground (ramp, bump): the slide ends, reserve kept. Landing with Space and steer
                    // held starts a new slide with a new locked direction - so wait for the landing as a Hop.
                    if (!i.Grounded)
                    {
                        EndSlide(ref s, ref ev);
                        s.Phase = DriftPhase.Hop;
                        s.PhaseTicks = h.HopTicks;
                        break;
                    }

                    // Charge: slip angle x speed x surface. One running total, capped at stage 3.
                    float slipDeg = Mathf.Atan2(Mathf.Abs(i.LateralSpeed), Mathf.Max(0.01f, speed)) * Mathf.Rad2Deg;
                    float slipFactor = SlipFactor(slipDeg, h.OptimalSlipAngle, h.MaxSlipAngle);
                    float speedFactor = Mathf.Lerp(h.SpeedChargeFloor, 1f, Mathf.Clamp01(speed / Mathf.Max(0.01f, h.TopSpeed)));
                    float gain = h.ReserveGainRate * slipFactor * speedFactor * Mathf.Max(0f, i.SurfaceChargeFactor);
                    s.Reserve = Mathf.Min(h.Stage3Threshold, s.Reserve + gain);

                    // Crossing a threshold opens that stage's window (once per stage per chain). A faster
                    // crossing replaces an open lower window with the higher one.
                    int stage = StageFor(s.Reserve, h);
                    if (stage > s.StageReached)
                    {
                        s.StageReached = stage;
                        s.WindowStage = stage;
                        s.WindowTicksRemaining = Mathf.Max(1, h.StageWindowTicks);
                        ev.WindowOpened = true;
                        ev.WindowStage = stage;
                    }

                    if (s.WindowTicksRemaining > 0)
                    {
                        if (boostPressed)
                        {
                            // Fire that stage. Still sliding, reserve untouched - it keeps climbing.
                            StartBoost(ref s, s.WindowStage, i.ForwardSpeed, h, i.Tick);
                            ev.BoostStarted = true;
                            ev.BoostStage = s.WindowStage;
                            CloseWindow(ref s);
                        }
                        else if (--s.WindowTicksRemaining == 0)
                        {
                            ev.WindowMissed = true; // only this stage is lost
                            s.WindowStage = 0;
                        }
                    }
                    // A Shift press with no window open is ignored, not punished.
                    break;
                }
        }

        // Reserve survives a break in the slide for StageResetTicks - the time to re-hop the other way.
        // Only time NOT sliding counts; any slide, in any direction, keeps the chain alive.
        if (s.Phase == DriftPhase.Sliding)
        {
            s.NotSlidingTicks = 0;
        }
        else if (s.Reserve > 0f || s.StageReached > 0)
        {
            s.NotSlidingTicks++;
            if (s.NotSlidingTicks >= h.StageResetTicks)
            {
                s.Reserve = 0f;
                s.StageReached = 0;
                s.NotSlidingTicks = 0;
                ev.ReserveLost = true;
            }
        }

        // The boost chain ends once a boost and its grace are over and this tick started nothing new.
        if (s.BoostTicksRemaining == 0 && s.ChainGraceTicksRemaining == 0)
            s.ChainLinks = 0;

        return ev;
    }

    static void EndSlide(ref DriftBoostState s, ref DriftBoostEvents ev)
    {
        if (s.WindowTicksRemaining > 0) ev.WindowMissed = true; // the slide ended with a window open
        CloseWindow(ref s);
        s.Phase = DriftPhase.Neutral;
        s.PhaseTicks = 0;
        s.Direction = 0;
        ev.DriftEnded = true;
        // Reserve and StageReached are kept; StageResetTicks ages them out.
    }

    static void CloseWindow(ref DriftBoostState s)
    {
        s.WindowStage = 0;
        s.WindowTicksRemaining = 0;
    }

    // ─── Direction lock ─────────────────────────────────────────────────

    // While Sliding, A/D only shape the arc; the car always rotates the locked way.
    //   steer into the drift  -> DriftArcTight (tightest)
    //   no steer              -> halfway (the neutral arc)
    //   steer fully against   -> DriftArcWide (widest) - never flips, never ends the slide
    // Not sliding: steering passes through unchanged.
    public static float SlideSteer(in DriftBoostState s, float steer, HandlingValues h)
    {
        if (!s.IsSliding || s.Direction == 0) return steer;
        float into = Mathf.Clamp(steer * s.Direction, -1f, 1f); // +1 into the drift, -1 against
        float arc = Mathf.Lerp(h.DriftArcWide, h.DriftArcTight, (into + 1f) * 0.5f);
        return s.Direction * arc;
    }

    // ─── Boost ──────────────────────────────────────────────────────────

    // Starts (or chains) a boost of the given stage 1..3. Stage windows and Overdrive both come through
    // here, so there is one boost economy. Overdrive: StartBoost(ref s, 2, speed, h, tick).
    public static void StartBoost(ref DriftBoostState s, int stage, float forwardSpeed, HandlingValues h, int tick)
    {
        float multiplier = BoostMultiplierFor(stage, h);
        int ticks = BoostTicksFor(stage, h);

        // Two boosts on the same tick = one chain link at the higher multiplier, never additive.
        if (s.IsBoosting && s.LastBoostStartTick == tick)
        {
            if (multiplier > s.BoostMultiplier) s.BoostMultiplier = multiplier;
            s.BoostTicksRemaining = Mathf.Max(s.BoostTicksRemaining, ticks);
            return;
        }

        bool chained = s.IsBoosting || s.ChainGraceTicksRemaining > 0 || s.ChainLinks > 0;
        s.ChainLinks = chained ? s.ChainLinks + 1 : 1;

        // Each chain link lifts the target from CURRENT speed, which is what carries the car
        // above nominal top speed. No clamp, no cap on chain length.
        s.BoostBaseSpeed = chained ? Mathf.Max(h.TopSpeed, forwardSpeed) : h.TopSpeed;
        s.BoostMultiplier = multiplier;
        s.BoostTicksRemaining = ticks;
        s.ChainGraceTicksRemaining = 0;
        s.LastBoostStartTick = tick;
    }

    static void TickBoostTimers(ref DriftBoostState s, HandlingValues h)
    {
        if (s.BoostTicksRemaining > 0)
        {
            s.BoostTicksRemaining--;
            if (s.BoostTicksRemaining == 0)
                s.ChainGraceTicksRemaining = h.ChainGraceTicks;
        }
        else if (s.ChainGraceTicksRemaining > 0)
        {
            s.ChainGraceTicksRemaining--;
        }
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
    public static float BumpMultiplier(in DriftBoostState s, HandlingValues h)
    {
        return (s.IsBoosting || s.IsChained || s.IsInChainGrace)
            ? h.BoostBumpMultiplier
            : 1f;
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    // Stage reached by the current reserve (0 = none).
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

    static int BoostTicksFor(int stage, HandlingValues h)
    {
        switch (stage)
        {
            case 3: return h.Stage3BoostTicks;
            case 2: return h.Stage2BoostTicks;
            default: return h.Stage1BoostTicks;
        }
    }

    // 1.0 at the optimal slip angle: rises up to it, falls off past it (a sideways drift charges slowly).
    static float SlipFactor(float angleDeg, float optimalDeg, float maxDeg)
    {
        if (angleDeg <= optimalDeg) return optimalDeg <= 0f ? 0f : angleDeg / optimalDeg;
        float t = (angleDeg - optimalDeg) / Mathf.Max(0.01f, maxDeg - optimalDeg);
        return Mathf.Lerp(1f, 0.1f, Mathf.Clamp01(t));
    }
}
