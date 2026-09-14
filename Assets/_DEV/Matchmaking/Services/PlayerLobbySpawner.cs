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

    // Six seats, one per MatchmakingConfig.MaxPlayers slot, laid out in a ring so nobody starts
    // advantaged (Glowtag FD-06's start-anchor requirement). Placeholder positions until the art
    // team's arena arrives with real authored anchors. PlayerId is assigned by Fusion and is the
    // same value on every peer, so the host placing player N always puts them in the same seat.
    static readonly Vector3[] SpawnOffsets =
    {
        new Vector3(0f, 1f, -6f),
        new Vector3(5f, 1f, -3f),
        new Vector3(5f, 1f, 3f),
        new Vector3(0f, 1f, 6f),
        new Vector3(-5f, 1f, 3f),
        new Vector3(-5f, 1f, -3f),
    };

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

        Vector3 spawnPosition = SpawnOffsets[player.PlayerId % SpawnOffsets.Length];
        runner.Spawn(playerCarPrefab, spawnPosition, Quaternion.identity, player);
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
