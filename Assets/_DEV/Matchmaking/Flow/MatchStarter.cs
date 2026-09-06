using UnityEngine;

// The achievable endpoint for this milestone: Game/ doesn't exist yet, so there's no real
// scene to load. This stub is the seam a future pass replaces with an actual scene-load /
// gameplay handoff, once a real player controller and gameplay scene exist.
public static class MatchStarter
{
    public static void StartMatch(int realPlayerCount, int botCount)
    {
        Debug.Log($"[Matchmaking] StartMatch — {realPlayerCount} real player(s) + {botCount} bot(s). Game/ scene not built yet.");
    }
}
