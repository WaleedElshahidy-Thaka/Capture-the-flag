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
    Loading,       // black screen: connecting, own car not yet spawned and seated
    Idle,          // in the arena, own robot visible, Quick Match button - nothing happens until pressed
    Searching,     // timer counting up; other searchers become visible the moment there are 2+
    WaitingSolo,   // timer >= 30s, still alone -> "start with computer players" (immediate)
    WaitingReady,  // shared timer >= 30s, 2+ searching -> ready toggle; all ready -> start with bots
    Starting       // MatchStarting observed true; StartMatch has been called
}
