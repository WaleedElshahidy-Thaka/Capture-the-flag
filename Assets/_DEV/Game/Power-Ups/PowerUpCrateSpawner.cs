
using Fusion;
using UnityEngine;

public class PowerUpCrateSpawner : MonoBehaviour
{
    [Header("Crate")]
    [SerializeField]
    private NetworkPrefabRef cratePrefab;

    [Header("Arena Spawn Points")]
    [SerializeField]
    private Transform[] spawnPoints;

    private bool hasSpawned;

    public bool SpawnForMatch(NetworkRunner runner)
    {
        if (runner == null || !runner.IsRunning)
        {
            Debug.LogError(
                "[PowerUpCrateSpawner] Runner is not running.");
            return false;
        }

        if (!HasSpawnAuthority(runner))
        {
            Debug.LogWarning(
                "[PowerUpCrateSpawner] This peer cannot spawn match crates.");
            return false;
        }

        if (hasSpawned)
            return true;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[PowerUpCrateSpawner] No spawn points assigned.");
            return false;
        }

        hasSpawned = true;

        foreach (Transform point in spawnPoints)
        {
            if (point == null)
            {
                Debug.LogError(
                    "[PowerUpCrateSpawner] A spawn point is missing.");
                continue;
            }
            Vector3 newPostion = new Vector3(point.position.x, 1 , point.position.z); // shift the crate up by 1 unit to avoid spawning inside the ground look flooting 
            
            NetworkObject crate = runner.Spawn(
                cratePrefab,
                newPostion,
                point.rotation
            );
            
            if (crate == null)
            {
                Debug.LogError(
                    $"[PowerUpCrateSpawner] Failed to spawn crate at {point.name}.");
                hasSpawned = false;
                return false;
            }
        }

        Debug.Log(
            $"[PowerUpCrateSpawner] Spawned {spawnPoints.Length} crates.");

        return true;
    }

    private static bool HasSpawnAuthority(NetworkRunner runner)
    {
        if (runner.GameMode == GameMode.Shared)
            return runner.IsSharedModeMasterClient;

        return runner.IsServer;
    }
}
