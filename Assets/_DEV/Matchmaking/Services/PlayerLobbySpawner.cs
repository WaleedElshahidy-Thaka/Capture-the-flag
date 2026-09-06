using System.Linq;
using Fusion;
using UnityEngine;

// Host-only: spawns a PlayerLobbyState for each player as they join, despawns it as they
// leave. A plain C# class (not a MonoBehaviour, matching this project's service-class
// convention), so the prefab reference comes from Resources.Load rather than a
// [SerializeField] slot - the same reason FusionRoomService never needed scene wiring either.
public class PlayerLobbySpawner
{
    const string PlayerLobbyStatePrefabName = "PlayerLobbyState";

    NetworkObject playerLobbyStatePrefab;

    public void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        if (playerLobbyStatePrefab == null)
            playerLobbyStatePrefab = Resources.Load<NetworkObject>(PlayerLobbyStatePrefabName);

        if (playerLobbyStatePrefab == null)
        {
            Debug.LogError($"[PlayerLobbySpawner] Resources/{PlayerLobbyStatePrefabName}.prefab not found.");
            return;
        }

        runner.Spawn(playerLobbyStatePrefab, inputAuthority: player);
    }

    public void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        var state = PlayerLobbyState.Active.FirstOrDefault(p => p.Object.InputAuthority == player);
        if (state != null) runner.Despawn(state.Object);
    }
}
