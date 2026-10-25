
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
        bool hasAuthority = runner.GameMode == GameMode.Shared
                            ? runner.IsSharedModeMasterClient
                            : runner.IsServer;

        if (!hasAuthority)
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
        /*
         Here is where you would set the initial holder of the crown. The code is commented out because it may not be necessary to randomly assign a crown holder at the start of the match, depending on your game design. If you want to assign a random player as the initial crown holder, you can uncomment the following lines:
         */

        //int index = Random.Range(0, eligibleCars.Count);
        //crown.SetInitialHolder(eligibleCars[index]);

        /*
         here is where you would spwan the crown in the scene by choose from the points that are in the scene, you can use the CrownSpawnerSelector to choose a point to spawn the crown. 
         */
        bool flowControl = HandleCrownSpawning(runner, matchObject, crown);
        if (!flowControl)
        {
            return;
        }

        flowControl = HandleCrateSpawning(runner);
        if (!flowControl)
        {
            return;
        }
    }

    private static bool HandleCrateSpawning(NetworkRunner runner)
    {
        PowerUpCrateSpawner crateSpawner = Object.FindFirstObjectByType<PowerUpCrateSpawner>();

        if (crateSpawner == null)
        {
            Debug.LogError(
                "[Match] PowerUpCrateSpawner is missing from the match scene.");
            return false;
        }

        if (!crateSpawner.SpawnForMatch(runner))
        {
            Debug.LogError(
                "[Match] Power-up crate spawning failed.");
            return false;
        }

        return true;
    }

    private static bool HandleCrownSpawning(NetworkRunner runner, NetworkObject matchObject, CrownMatchState crown)
    {
        CrownSpawnManager crownSpawner =
               Object.FindFirstObjectByType<CrownSpawnManager>();

        if (crownSpawner == null)
        {
            Debug.LogError(
                "[Match] CrownSpawnManager is missing from the match scene.");
            runner.Despawn(matchObject);
            return false;
        }

        if (!crownSpawner.SpawnCrown(runner, crown))
        {
            Debug.LogError("[Match] Crown spawning failed.");
            runner.Despawn(matchObject);
            return false;
        }
        Debug.Log("[Match] Crown and timer initialized.");
        return true;
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
