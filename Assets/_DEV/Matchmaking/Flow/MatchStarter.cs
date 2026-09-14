using UnityEngine;

// The gameplay handoff, local side. The real PlayerCar already exists and is positioned by the
// time this runs (spawned at connect time by PlayerLobbySpawner), and the movement gate is
// released by the host (MatchmakingSessionState.TriggerStart) - so all that's left here is
// taking the matchmaking UI off screen. No scene load involved (one scene throughout).
//
// Bots (botCount) aren't handled yet - no bot AI exists in this build, same gap noted in
// Documentation/Networking_Progress.md. A bot-filled slot currently just doesn't get a car.
public static class MatchStarter
{
    // Local presentation only. The movement gate (PlayerMatchState.CanMove) is released
    // host-side in MatchmakingSessionState.TriggerStart and replicates from there - a client
    // holds only Input Authority over its car, not State Authority, so it cannot write that
    // flag itself. This runs on every peer purely to take the matchmaking UI off screen.
    public static void StartMatch(int realPlayerCount, int botCount)
    {
        var screen = Object.FindFirstObjectByType<MatchmakingScreen>();
        if (screen != null)
        {
            var canvas = screen.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        Debug.Log($"[Matchmaking] StartMatch — {realPlayerCount} real player(s) + {botCount} bot(s).");
    }
}
