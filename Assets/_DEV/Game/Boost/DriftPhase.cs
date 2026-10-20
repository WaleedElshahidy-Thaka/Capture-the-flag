
// Layer C of the Driving Foundation: the drift -> boost economy (Drift & Boost Economy v0.3,
// as answered in "Glowtag Drift, Boost & Overdrive: Implementation Answers v0.1").
//
// Pure and tick-based, same pattern as DriveState: PlayerMovement keeps these fields [Networked],
// copies them into a DriftBoostState, calls Tick once per simulated tick, writes them back.
// No Unity objects, no randomness, no wall-clock time - a re-simulated tick lands on the same result.
//
// Flow:  Neutral --hop (Space press, grounded, >= drift_min_speed)--> Hop (hop_ticks)
//        --land, Space still held + steer held--> Sliding (charges reserve)
//        --release Space / leave ground / contact--> Release window (window_ticks)
//        --Left Shift--> Boosting (stage from reserve)   or   window ends --> Fizzle (reserve lost)
//        A new boost while Boosting or within chain_grace_ticks of one ending = a chain link.
//
// Changes from v1:
//  - BumpMultiplier now also applies during Chained (including the chain grace period), per the spec.
//  - Reset() added for respawn (clears reserve AND chain).
//  - CurrentStage() added so UI/VFX can show the "Charged" sub-state of Sliding.

public enum DriftPhase : byte
{
    Neutral = 0,
    Hop = 1,
    Sliding = 2, // "Charged" is just a stage reached while Sliding
    Window = 3   // release window
}
