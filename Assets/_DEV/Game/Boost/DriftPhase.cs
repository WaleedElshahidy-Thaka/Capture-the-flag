
// Layer C of the Driving Foundation: the drift -> boost economy, Hold-to-Boost Model v0.1 with
// Direction Lock & Control Split v0.1 (replaces the v0.3 release-window loop).
//
// Pure and tick-based, same pattern as DriveState: PlayerMovement keeps these fields [Networked],
// copies them into a DriftBoostState, calls Tick once per simulated tick, writes them back.
// No Unity objects, no randomness, no wall-clock time - a re-simulated tick lands on the same result.
//
// Controls:  Space = the slide (hop, hold, release ends it)   Shift = boost timing only
//            A/D   = steering; during a slide it only shapes the arc, never flips it
//
// Flow:  Neutral --Space press (grounded, >= DriftMinSpeed)--> Hop (HopTicks)
//        --land with Space held + steer held--> Sliding, direction LOCKED, reserve charges
//        each threshold crossed opens a stage window (StageWindowTicks):
//            Shift inside it -> that stage's boost, STILL SLIDING; miss it -> only that stage is lost
//        --release Space / drop below DriftMinSpeed / leave the ground--> slide ends
//        Reserve is one running total: firing a boost doesn't spend it, and it survives a break
//        in the slide for StageResetTicks (re-hop the other way and keep climbing).
//        A new boost while Boosting or within ChainGraceTicks of one ending = a chain link.

public enum DriftPhase : byte
{
    Neutral = 0,
    Hop = 1,
    Sliding = 2, // stage windows are a sub-state of Sliding (DriftBoostState.InWindow)
}
