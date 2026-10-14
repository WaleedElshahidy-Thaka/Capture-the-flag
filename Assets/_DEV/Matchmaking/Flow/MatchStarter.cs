using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Local hook for the moment a match starts. Cars already exist and the host has already released
// CanMove, so for now this only logs. Bot cars are not spawned yet (no bot AI in this build).
public static class MatchStarter
{
    public static void StartMatch(int realPlayerCount, int botCount)
    {
        Debug.Log($"[Matchmaking] StartMatch — {realPlayerCount} real player(s) + {botCount} bot(s).");
    }
    public static void StartMatch( NetworkRunner runner, int realPlayerCount, int botCount)
    {
        if (runner == null || !runner.IsRunning)
            return;

        Debug.Log(
            $"[Matchmaking] StartMatch — {realPlayerCount} real player(s) + {botCount} bot(s).");

        // Only the host creates and initializes the Crown state.
        if (!runner.IsServer)
            return;

        // A restored Crown state may already exist after host migration.
        if (CrownMatchState.Instance != null)
            return;

        NetworkObject prefab =
            Resources.Load<NetworkObject>("CrownMatchState");

        if (prefab == null)
        {
            Debug.LogError(
                "[Crown] Resources/CrownMatchState.prefab not found.");
            return;
        }

        List<PlayerMatchState> players = PlayerRoster.Active.ToList();
        var eligibleCars = new List<NetworkObject>();

        for (int i = 0; i < players.Count; i++)
        {
            PlayerMatchState player = players[i];

            if (player == null || player.Object == null)
                continue;

            // For this first version, choose a real player,
            // not a bot.
            if (!player.IsBot)
                eligibleCars.Add(player.Object);
        }

        if (eligibleCars.Count == 0)
        {
            Debug.LogWarning(
                "[Crown] No eligible player cars found yet.");
            return;
        }

        NetworkObject crownObject = runner.Spawn(prefab);

        CrownMatchState crown =
            crownObject.GetComponent<CrownMatchState>();

        if (crown == null)
        {
            Debug.LogError(
                "[Crown] CrownMatchState component is missing from its prefab.");
            runner.Despawn(crownObject);
            return;
        }

        int chosenIndex = Random.Range(0, eligibleCars.Count);
        crown.SetInitialHolder(eligibleCars[chosenIndex]);
    }
}
