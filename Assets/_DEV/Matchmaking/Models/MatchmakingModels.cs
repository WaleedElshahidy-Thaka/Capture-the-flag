// Ok/Fail result-type convention, matching Future_DEV/Scripts/Models/GameModels.cs.
public class StartQuickMatchResult
{
    public bool Success { get; private set; }
    public IActiveQuickMatchSession Session { get; private set; }
    public string Error { get; private set; }

    public static StartQuickMatchResult Ok(IActiveQuickMatchSession session) => new StartQuickMatchResult { Success = true, Session = session };
    public static StartQuickMatchResult Fail(string error) => new StartQuickMatchResult { Success = false, Error = error };
}

// Drives which panel MatchmakingScreen shows. Owned by MatchmakingFlowController.
public enum MatchmakingPhase
{
    Connecting,    // scene just started, still joining the shared hub - Find Match disabled
    Idle,          // connected to the hub, but not searching - before "Find Match" is pressed
    Searching,     // 0-120s elapsed, room not full yet
    WaitingSolo,   // timer expired, still alone -> "start with computer players" button
    WaitingReady,  // timer expired, 2-3 real players present -> per-player ready toggle
    Starting       // MatchStarting observed true; StartMatch has been called
}
