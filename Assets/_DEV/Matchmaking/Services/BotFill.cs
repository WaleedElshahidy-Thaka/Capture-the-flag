using Fusion;
using UnityEngine;

// Host only: at match start, puts a bot car in every seat no player is sitting in (FD-07, lobby Bot
// Fill). A bot is a normal PlayerCar with no input authority - PlayerMatchState.IsBot is true, so
// PlayerMovement drives it through BotDriver/BotBrain like any player car.
//
// Bots are spawned once, at StartMatch, and never replaced: a player who drops mid-match keeps their
// car, which is handed to the bot driver instead (PlayerLobbySpawner.HandlePlayerLeft).
public static class BotFill
{
    const string PlayerCarPrefabName = "PlayerCar";

    // Returns how many bots were actually seated (fewer than asked if seats ran out).
    public static int SpawnBots(NetworkRunner runner, int botCount)
    {
        if (runner == null || !runner.IsServer || botCount <= 0) return 0;

        var prefab = Resources.Load<NetworkObject>(PlayerCarPrefabName);
        if (prefab == null)
        {
            Debug.LogError($"[BotFill] Resources/{PlayerCarPrefabName}.prefab not found - no bots spawned.");
            return 0;
        }

        int spawned = 0;

        // Lowest free seat first, so the same lobby always produces the same layout.
        for (int slot = 0; slot < LobbyLayout.SlotCount && spawned < botCount; slot++)
        {
            if (LobbySeats.IsTaken(slot)) continue;

            int seat = slot;
            int number = spawned + 1;

            NetworkObject bot = runner.Spawn(prefab, LobbyLayout.SlotPosition(seat), LobbyLayout.SlotRotation,
                inputAuthority: null,
                onBeforeSpawned: (r, obj) =>
                {
                    var state = obj.GetComponent<PlayerMatchState>();
                    state.Slot = seat;
                    state.DisplayName = $"Bot {number}"; // the scoreboard/results label bots themselves
                    state.OwnerToken = "";                 // empty = nobody can ever reclaim it
                    state.WasHost = false;
                    state.CanMove = true;                  // released with everyone else
                });

            if (bot == null)
            {
                Debug.LogError($"[BotFill] Failed to spawn a bot in seat {seat}.");
                continue;
            }

            spawned++;
        }

        if (spawned < botCount)
            Debug.LogWarning($"[BotFill] Asked for {botCount} bot(s), only {spawned} free seat(s).");

        Debug.Log($"[BotFill] Seated {spawned} bot(s).");
        return spawned;
    }
}
