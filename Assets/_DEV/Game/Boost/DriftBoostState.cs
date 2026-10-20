public struct DriftBoostState
{
    public DriftPhase Phase;
    public int PhaseTicks;           // ticks spent in Hop or Window
    public int Direction;            // -1 / +1, committed at landing
    public float Reserve;            // charge units
    public bool HopHeldLastTick;     // for the hop press edge
    public int BoostTicksRemaining;
    public float BoostMultiplier;    // x BoostBaseSpeed
    public float BoostBaseSpeed;     // TopSpeed, or current speed when chaining
    public int ChainGraceTicksRemaining;
    public int ChainLinks;           // 0 = no chain, 1 = single boost, 2+ = chained
    public int LastBoostStartTick;   // same-tick boosts merge into one link

    public bool IsHopping => Phase == DriftPhase.Hop;
    public bool IsSliding => Phase == DriftPhase.Sliding;
    public bool InWindow => Phase == DriftPhase.Window;
    public bool IsBoosting => BoostTicksRemaining > 0;
    public bool IsChained => ChainLinks > 1;
    public bool IsInChainGrace => ChainGraceTicksRemaining > 0;
}
