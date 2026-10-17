
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public static class MatchStarter
{
    public static void StartMatch(
        NetworkRunner runner,
        int realPlayerCount,
        int botCount)
    {
        if (runner == null || !runner.IsRunning)
            return;

        Debug.Log(
            $"[Matchmaking] Starting match: {realPlayerCount} player(s), " +
            $"{botCount} bot(s).");

        // Only the server/host may spawn the shared match state.
        if (!runner.IsServer)
            return;

        // Prevent spawning another match state if one already exists.
        if (CrownMatchState.Instance != null)
            return;

        NetworkObject prefab =
            Resources.Load<NetworkObject>("CrownMatchState");

        if (prefab == null)
        {
            Debug.LogError(
                "[Match] Missing Resources/CrownMatchState.prefab.");
            return;
        }

        List<PlayerMatchState> players =
            new List<PlayerMatchState>(PlayerRoster.Active);

        var eligibleCars = new List<NetworkObject>();

        foreach (PlayerMatchState player in players)
        {
            if (player == null || player.Object == null)
                continue;

            if (!player.IsBot)
                eligibleCars.Add(player.Object);
        }

        if (eligibleCars.Count == 0)
        {
            Debug.LogError(
                "[Match] No eligible player cars found. " +
                "Check that PlayerRoster is populated before StartMatch.");
            return;
        }

        NetworkObject matchObject = runner.Spawn(prefab);

        if (matchObject == null)
        {
            Debug.LogError("[Match] Failed to spawn match state.");
            return;
        }

        CrownMatchState crown =
            matchObject.GetComponent<CrownMatchState>();

        MatchTimerState timer =
            matchObject.GetComponent<MatchTimerState>();

        if (crown == null || timer == null)
        {
            Debug.LogError(
                "[Match] CrownMatchState prefab must contain both " +
                "CrownMatchState and MatchTimerState.");

            runner.Despawn(matchObject);
            return;
        }

        int index = Random.Range(0, eligibleCars.Count);
        crown.SetInitialHolder(eligibleCars[index]);

        Debug.Log("[Match] Crown and timer initialized.");
    }

    // Keep this only if another part of your project still calls it.
    // It does not initialize a networked match.
    public static void StartMatch(int realPlayerCount, int botCount)
    {
        Debug.LogWarning(
            "[Match] The non-networked StartMatch overload was called. " +
            "Use StartMatch(NetworkRunner, int, int) instead.");
    }
}
