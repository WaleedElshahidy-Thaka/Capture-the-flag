using Fusion;
using UnityEngine;

// Shared Mode: each peer spawns its OWN PlayerCar in response to its OWN join - spawning is
// what grants State Authority in Shared Mode, and a peer can't despawn an object it doesn't
// have authority over anyway, so there's no "host" left to do this on everyone's behalf. The
// car exists (positioned, visible) from connect time; PlayerMatchState.CanMove is what actually
// gates driving until a match starts (see MatchStarter) - there's no separate lobby-only
// placeholder object anymore. A plain C# class (not a MonoBehaviour, matching this project's
// service-class convention), so the prefab reference comes from Resources.Load rather than a
// [SerializeField] slot.
public class PlayerLobbySpawner
{
    const string PlayerCarPrefabName = "PlayerCar";

    // MatchmakingConfig.MaxPlayers is 4 - one offset per seat, spread out so cars don't spawn
    // stacked on top of each other. PlayerId is assigned by Fusion and is the same value on
    // every peer, unlike a locally-computed list index.
    static readonly Vector3[] SpawnOffsets =
    {
        new Vector3(0f, 1f, 0f),
        new Vector3(-4f, 1f, 0f),
        new Vector3(4f, 1f, 0f),
        new Vector3(0f, 1f, 4f),
    };

    NetworkObject playerCarPrefab;

    public void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // This fires on every peer for every player that joins (including remote ones) - only
        // ever spawn in response to MY OWN join.
        if (player != runner.LocalPlayer) return;

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

    // No manual despawn-on-leave - a peer can only despawn objects it has State Authority over,
    // and under Shared Mode that's only ever your own PlayerCar. Cleanup when a player
    // disconnects relies on that prefab's NetworkObject having "Destroy When State Authority
    // Leaves" enabled in the Inspector - verify that's checked on
    // Game/Player/Resources/PlayerCar.prefab rather than assuming it.
    public void HandlePlayerLeft(NetworkRunner runner, PlayerRef player) { }
}
