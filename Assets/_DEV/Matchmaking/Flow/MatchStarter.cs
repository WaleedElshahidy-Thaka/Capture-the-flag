using UnityEngine;

// The gameplay handoff, local side. The real PlayerCar already exists and is positioned by the
// time this runs (spawned at connect time by PlayerLobbySpawner), and the movement gate is
// released by the host (MatchmakingSessionState.TriggerStart) - so there is nothing left to do
// here but note it. The matchmaking panels come off screen through the Starting phase
// (MatchmakingScreen); the canvas itself stays, because the host-migration overlays live on it
// and have to be able to appear mid-match.
//
// Bots (botCount) aren't handled yet - no bot AI exists in this build, same gap noted in
// Documentation/Networking_Progress.md. A bot-filled slot currently just doesn't get a car.
public static class MatchStarter
{
    public static void StartMatch(int realPlayerCount, int botCount)
    {
        Debug.Log($"[Matchmaking] StartMatch — {realPlayerCount} real player(s) + {botCount} bot(s).");
    }
}
