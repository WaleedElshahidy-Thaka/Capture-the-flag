using UnityEngine;

// When a match starts - the one rule both the host (MatchmakingSessionState) and the lobby countdown
// (LobbyUI) use, so the number players see is the number that starts the match.
//
//   1. The countdown runs from 120 s to 0 (SearchDurationSeconds). At 0 the match starts, bots
//      filling the empty seats - solo or with others.
//   2. If all six seats are taken by players who pressed READY, it starts at once, even with time
//      left on the countdown.
//
// "elapsed" is the search timer: your own while alone, the lobby's shared timer once together.
// "players" is everyone who pressed READY and is in the lobby (alone = 1).
// SecondsOffPerExtraPlayer can still shorten the countdown per extra player; it's 0 for now.
public static class MatchStartRule
{
    public static float SecondsUntilStart(float elapsedSeconds, int players)
    {
        if (IsFull(players)) return 0f;

        int extra = Mathf.Max(0, players - 1);
        float total = MatchmakingConfig.SearchDurationSeconds - extra * MatchmakingConfig.SecondsOffPerExtraPlayer;
        return Mathf.Max(0f, total - elapsedSeconds);
    }

    public static bool ShouldStart(float elapsedSeconds, int players) =>
        IsFull(players) || SecondsUntilStart(elapsedSeconds, players) <= 0f;

    // Six ready players: nothing left to wait for.
    public static bool IsFull(int players) => players >= MatchmakingConfig.MaxPlayers;
}
