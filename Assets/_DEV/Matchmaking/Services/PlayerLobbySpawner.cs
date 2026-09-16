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
public class PlayerLobbySpawner
{
    const string PlayerCarPrefabName = "PlayerCar";

    NetworkObject playerCarPrefab;

    public void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

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
        runner.Spawn(playerCarPrefab, LobbyLayout.SlotPosition(slot), LobbyLayout.SlotRotation, player);
    }

    // The host owns every car, so it is also the peer that despawns one when its player leaves.
    public void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        foreach (var state in PlayerMatchState.Active)
        {
            if (state.Object.InputAuthority != player) continue;
            runner.Despawn(state.Object);
            return;
        }
    }
}
