
// What happened this tick, for cosmetics and audio only. Never feed back into the simulation.
public struct DriftBoostEvents
{
    public bool HopStarted;
    public bool DriftStarted;    // landed with steer held: direction locked
    public bool DriftEnded;      // Space released, too slow, or left the ground
    public bool WindowOpened;    // a stage threshold was crossed - cue the "press now" prompt
    public int WindowStage;      // ...which stage (1..3)
    public bool BoostStarted;
    public int BoostStage;       // 1..3
    public bool WindowMissed;    // a window closed unused - only that stage is lost
    public bool ReserveLost;     // out of a slide for StageResetTicks: reserve and stages zeroed
}
