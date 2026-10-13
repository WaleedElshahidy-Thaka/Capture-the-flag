using UnityEngine;

// Local hook for the moment a match starts. Cars already exist and the host has already released
// CanMove, so for now this only logs. Bot cars are not spawned yet (no bot AI in this build).
public static class MatchStarter
{
    public static void StartMatch(int realPlayerCount, int botCount)
    {
        Debug.Log($"[Matchmaking] StartMatch — {realPlayerCount} real player(s) + {botCount} bot(s).");
    }
}
