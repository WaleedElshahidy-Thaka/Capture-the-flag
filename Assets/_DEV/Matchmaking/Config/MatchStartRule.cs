// When a match starts - the one rule both the host (MatchmakingSessionState) and the lobby screen
// (LobbyUI) use, so the screen always says what the host will do.
//
//   0 s .. 30 s    searching; no choice yet
//   30 s ..        alone      -> START WITH BOTS (PlayerMatchState.RPC_RequestStartWithBots)
//                  with others -> READY (a vote); everyone in the lobby ready = start now
//   120 s          start in any case
// Bots always fill the empty seats.
//
// "elapsed" is the search timer: your own while alone, the lobby's shared timer once together.
public static class MatchStartRule
{
    // The choice (bots / ready) is available.
    public static bool ChoiceUnlocked(float elapsedSeconds) =>
        elapsedSeconds >= MatchmakingConfig.BotOptionUnlockSeconds;

    // The timer has run out: start no matter what.
    public static bool TimeUp(float elapsedSeconds) =>
        elapsedSeconds >= MatchmakingConfig.SearchDurationSeconds;

    // With other players: start when the time is up, or after the unlock once everyone has voted.
    public static bool ShouldStartTogether(float elapsedSeconds, bool everyoneReady) =>
        TimeUp(elapsedSeconds) || (ChoiceUnlocked(elapsedSeconds) && everyoneReady);

    // Alone: only the timer (START WITH BOTS is an explicit request, checked by the host).
    public static bool ShouldStartAlone(float elapsedSeconds) => TimeUp(elapsedSeconds);
}
