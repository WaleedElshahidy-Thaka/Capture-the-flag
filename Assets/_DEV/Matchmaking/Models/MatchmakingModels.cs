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
    Idle,          // offline: the arena, a local preview of your robot, Quick Match
    Searching,     // timer from the click: connecting behind it, then own timer, then the shared one in the lobby
    Found,         // "Player found! Joining lobby in N" - the 3 s countdown before the lobby
    WaitingSolo,   // timer >= 30s, still alone -> "start with computer players" (immediate)
    WaitingReady,  // shared timer >= 30s, 2+ searching -> ready toggle; all ready -> start with bots
    Starting,      // the match is on - every matchmaking panel is off screen
    Reconnecting,  // the host dropped: Fusion is electing a new one and this peer is rejoining
    Resuming       // rejoined; everyone unfreezes together at the end of the shared countdown
}
