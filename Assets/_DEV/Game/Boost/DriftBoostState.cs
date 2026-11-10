public struct DriftBoostState
{
    public DriftPhase Phase;
    public int PhaseTicks;           // ticks spent in Hop
    public int Direction;            // -1 / +1, locked at landing for the whole slide; 0 when not sliding
    public float Reserve;            // charge units - ONE running total, never spent by a boost
    public bool HopHeldLastTick;     // Space press edge
    public bool BoostHeldLastTick;   // Shift press edge: each stage needs its own press

    public int StageReached;         // highest threshold crossed this chain (0..3); each opens one window
    public int WindowStage;          // stage whose window is open (0 = none)
    public int WindowTicksRemaining; // StageWindowTicks counting down
    public int NotSlidingTicks;      // time out of a slide; reserve and stages zero at StageResetTicks

    public int BoostTicksRemaining;
    public float BoostMultiplier;    // x BoostBaseSpeed
    public float BoostBaseSpeed;     // TopSpeed, or current speed when chaining
    public int ChainGraceTicksRemaining;
    public int ChainLinks;           // 0 = no chain, 1 = single boost, 2+ = chained
    public int LastBoostStartTick;   // same-tick boosts merge into one link

    public bool IsHopping => Phase == DriftPhase.Hop;
    public bool IsSliding => Phase == DriftPhase.Sliding;
    public bool InWindow => Phase == DriftPhase.Sliding && WindowTicksRemaining > 0;
    public bool IsBoosting => BoostTicksRemaining > 0;
    public bool IsChained => ChainLinks > 1;
    public bool IsInChainGrace => ChainGraceTicksRemaining > 0;
}
