// Where the local player is in the Quick Match flow. Owned by MatchmakingFlowController,
// displayed by MatchmakingScreen.
public enum MatchmakingPhase
{
    Idle,          // offline: arena, local preview robot, Quick Match button
    Searching,     // timer running: connecting, then own timer, then the shared lobby timer
    Found,         // "Player found! Joining lobby in N"
    WaitingSolo,   // timer >= 30 s and still alone -> "start with computer players"
    WaitingReady,  // shared timer >= 30 s, 2+ players -> ready toggle; all ready -> start with bots
    Starting,      // match is on; matchmaking panels hidden
    Reconnecting,  // host dropped; rejoining the new host
    Resuming       // rejoined; everyone unfreezes together after a countdown
}

public static class MatchmakingPhaseExtensions
{
    // Phases that show the search timer panel.
    public static bool ShowsSearchTimer(this MatchmakingPhase phase) =>
        phase == MatchmakingPhase.Searching || phase == MatchmakingPhase.WaitingSolo || phase == MatchmakingPhase.WaitingReady;

    // Phases before the match where the player can still back out.
    public static bool IsCancellable(this MatchmakingPhase phase) =>
        phase.ShowsSearchTimer() || phase == MatchmakingPhase.Found;

    // Phases spent in the pre-match lobby (offline or searching).
    public static bool IsPreMatch(this MatchmakingPhase phase) =>
        phase == MatchmakingPhase.Idle || phase.IsCancellable();
}
