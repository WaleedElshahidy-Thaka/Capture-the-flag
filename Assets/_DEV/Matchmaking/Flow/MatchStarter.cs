using UnityEngine;

// The gameplay handoff. The real PlayerCar already exists and is positioned by the time this
// runs (spawned at connect time by PlayerLobbySpawner, not here) - all this does is flip on the
// local player's own PlayerMatchState.CanMove and hide the matchmaking UI. No scene load
// involved (one scene throughout, per the design).
//
// Bots (botCount) aren't handled yet - no bot AI exists in this build, same gap noted in
// Documentation/Networking_Progress.md. A bot-filled slot currently just doesn't get a car.
public static class MatchStarter
{
    public static void StartMatch(int realPlayerCount, int botCount)
    {
        var localState = PlayerMatchState.Local;
        if (localState == null)
        {
            Debug.LogWarning("[MatchStarter] No local PlayerMatchState - can't start driving (solo/local backend, or not connected yet?).");
            return;
        }

        localState.CanMove = true;

        var screen = Object.FindFirstObjectByType<MatchmakingScreen>();
        if (screen != null)
        {
            var canvas = screen.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        Debug.Log($"[Matchmaking] StartMatch — {realPlayerCount} real player(s) + {botCount} bot(s).");
    }
}
