using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Host only: gives every joining player a car (seated in the lobby row, not drivable until
// CanMove) and decides what happens to it when they leave.
// Cars outlive their players: a player whose install token matches an ownerless car (everyone
// reconnecting after a host migration) gets that car back. A player who leaves mid-match leaves
// their car to the bot driver so the field stays at six.
public class PlayerLobbySpawner
{
    const string PlayerCarPrefabName = "PlayerCar";

    NetworkObject playerCarPrefab;

    // Players who joined a resumed session before the snapshot's cars existed again.
    readonly Dictionary<PlayerRef, string> pendingReclaims = new Dictionary<PlayerRef, string>();

    public PlayerLobbySpawner()
    {
        MatchmakingSessionState.HostMigrationRestored += SettlePending;
    }

    public void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        string token = PlayerIdentity.TokenText(runner.GetPlayerConnectionToken(player));

        if (runner.IsResume && MatchmakingSessionState.Local == null)
        {
            pendingReclaims[player] = token;
            return;
        }

        Seat(runner, player, token);
    }

    // In the lobby the car goes; mid-match it stays and is driven by the bot.
    public void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        pendingReclaims.Remove(player);

        var car = PlayerRoster.FindOwnedBy(player);
        if (car == null) return;

        if (IsMatchInProgress) car.HandToBot();
        else runner.Despawn(car.Object);
    }

    static bool IsMatchInProgress
    {
        get
        {
            var session = MatchmakingSessionState.Local;
            return session != null && session.MatchStarting;
        }
    }

    void SettlePending(NetworkRunner runner)
    {
        foreach (var pending in pendingReclaims)
            Seat(runner, pending.Key, pending.Value);
        pendingReclaims.Clear();
    }

    void Seat(NetworkRunner runner, PlayerRef player, string token)
    {
        bool isHost = player == runner.LocalPlayer;

        var reclaimed = PlayerRoster.FindReclaimable(token);
        if (reclaimed != null)
        {
            reclaimed.Object.AssignInputAuthority(player);
            reclaimed.WasHost = isHost;
            return;
        }

        // The session is closed during a match, so this only happens if something slipped through.
        if (IsMatchInProgress)
        {
            Debug.LogWarning($"[PlayerLobbySpawner] {player} joined a match in progress with no car to reclaim - ignored.");
            return;
        }

        SpawnCar(runner, player, token, isHost);
    }

    void SpawnCar(NetworkRunner runner, PlayerRef player, string token, bool isHost)
    {
        if (playerCarPrefab == null)
            playerCarPrefab = Resources.Load<NetworkObject>(PlayerCarPrefabName);

        if (playerCarPrefab == null)
        {
            Debug.LogError($"[PlayerLobbySpawner] Resources/{PlayerCarPrefabName}.prefab not found.");
            return;
        }

        // Player N prefers seat N; if someone already sits there (PlayerIds keep counting up as people
        // come and go), the next free seat. Two cars must never share a seat - bots fill the rest.
        int slot = LobbySeats.FirstFree(player.PlayerId % LobbyLayout.SlotCount);
        if (slot < 0)
        {
            Debug.LogWarning($"[PlayerLobbySpawner] No free seat for {player} - lobby is full.");
            return;
        }

        runner.Spawn(playerCarPrefab, LobbyLayout.SlotPosition(slot), LobbyLayout.SlotRotation, player,
            onBeforeSpawned: (r, obj) =>
            {
                var state = obj.GetComponent<PlayerMatchState>();
                state.OwnerToken = token;
                state.WasHost = isHost;
                state.Slot = slot;
            });
    }
}
