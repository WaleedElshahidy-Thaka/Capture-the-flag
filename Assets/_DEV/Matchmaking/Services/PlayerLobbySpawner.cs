using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Host/Client (GameMode.AutoHostOrClient): the host is the only peer that spawns, and it spawns
// one PlayerCar for every player who joins - itself included - handing each one Input Authority
// over their own car. Clients never call Spawn; they receive the cars by replication.
//
// This is the inverse of the Shared Mode arrangement an earlier pass used (each peer spawning
// its own car on its own join). The switch matters because state authority over every car now
// lives on one peer, which is what lets a chassis-to-chassis contact be resolved once, by one
// authority, rather than twice with two disagreeing answers - see QuickMatchFusionService's doc
// comment and GDD doc 05.
//
// The car exists (positioned, visible) from connect time; PlayerMatchState.CanMove is what gates
// driving until a match starts. A plain C# class (not a MonoBehaviour, matching this project's
// service-class convention), so the prefab reference comes from Resources.Load rather than a
// [SerializeField] slot.
//
// Cars outlive their players. Every car carries its owner's install id (OwnerToken); a player
// who joins with a token that matches an ownerless car - which is every player reconnecting
// after a host migration - gets that car back rather than a new one. A player who leaves
// mid-match leaves their car behind for the bot driver (FD-07: the field stays at six).
public class PlayerLobbySpawner
{
    const string PlayerCarPrefabName = "PlayerCar";

    NetworkObject playerCarPrefab;

    // Players who joined a resumed session before the snapshot's cars existed again - the new
    // host itself, typically, whose own join can land before the resume finishes. Settled once
    // MatchmakingSessionState reports the restore complete.
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

    void SettlePending(NetworkRunner runner)
    {
        foreach (var pending in pendingReclaims)
            Seat(runner, pending.Key, pending.Value);
        pendingReclaims.Clear();
    }

    void Seat(NetworkRunner runner, PlayerRef player, string token)
    {
        bool isHost = player == runner.LocalPlayer;

        if (TryReclaimCar(token, out var car))
        {
            car.Object.AssignInputAuthority(player);
            car.WasHost = isHost;
            return;
        }

        // A match in progress admits nobody new. The session is closed to matchmaking by then,
        // so this only happens if something slipped through - they get no car and no match.
        var session = MatchmakingSessionState.Local;
        if (session != null && session.MatchStarting)
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

        // Seat = PlayerId, which Fusion assigns and which is the same value on every peer, so
        // the host placing player N always puts them in the same seat of LobbyLayout's row.
        int slot = player.PlayerId % LobbyLayout.SlotCount;
        runner.Spawn(playerCarPrefab, LobbyLayout.SlotPosition(slot), LobbyLayout.SlotRotation, player,
            onBeforeSpawned: (r, obj) =>
            {
                var state = obj.GetComponent<PlayerMatchState>();
                state.OwnerToken = token;
                state.WasHost = isHost;
            });
    }

    static bool TryReclaimCar(string token, out PlayerMatchState car)
    {
        car = null;
        if (string.IsNullOrEmpty(token)) return false;

        foreach (var state in PlayerMatchState.Active)
        {
            if (!state.IsBot || state.OwnerToken != token) continue;
            car = state;
            return true;
        }
        return false;
    }

    // The host owns every car, so it is also the peer that decides what happens to one when its
    // player leaves. In the lobby the car simply goes. Mid-match it stays and drives itself -
    // a vanishing car would drop the field below six, and would delete the score and Crown
    // status the game later hangs on it.
    public void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        pendingReclaims.Remove(player);

        var session = MatchmakingSessionState.Local;
        bool inMatch = session != null && session.MatchStarting;

        foreach (var state in PlayerMatchState.Active)
        {
            if (state.Object.InputAuthority != player) continue;

            if (inMatch) state.HandToBot();
            else runner.Despawn(state.Object);
            return;
        }
    }
}
