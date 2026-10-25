using Fusion;
using System.Data;
using UnityEngine;

public class CrownSpawnManager : MonoBehaviour
{
    [Header("Authored Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Crown Prefab")]
    [SerializeField] private NetworkObject crownPrefab;

    [Header("Spawn Rules")]
    [SerializeField, Min(0f)]
    private float minSpawnDistance = 5f;

    [SerializeField, Range(0.55f, 0.85f)]
    private float spawnAsymmetryRatio = 0.7f;

    [SerializeField, Min(0f)]
    private float telegraphDuration = 3f;

    public bool SpawnCrown(
        NetworkRunner runner,
        CrownMatchState matchState)
    {
        if (runner == null || matchState == null)
            return false;

        if (!HasSpawnAuthority(runner))
            return false;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[CrownSpawnManager] No spawn points assigned.");
            return false;
        }

        if (crownPrefab == null)
        {
            Debug.LogError(
                "[CrownSpawnManager] Crown prefab is missing.");
            return false;
        }

        var players = new System.Collections.Generic.List<PlayerMatchState>(
            PlayerRoster.Active);

        Transform selectedPoint = CrownSpawnSelector.Select(
            spawnPoints,
            players,
            runner.Tick,
            minSpawnDistance,
            spawnAsymmetryRatio);

        if (selectedPoint == null)
        {
            Debug.LogError(
                "[CrownSpawnManager] No valid Crown spawn point found.");
            return false;
        }

        NetworkObject crownObject = runner.Spawn(
            crownPrefab,
            selectedPoint.position,
            selectedPoint.rotation);

        if (crownObject == null)
        {
            Debug.LogError(
                "[CrownSpawnManager] Failed to spawn Crown.");
            return false;
        }

        DataCrown dataCrown = crownObject.GetComponent<DataCrown>();

        if (dataCrown == null)
        {
            Debug.LogError(
                "[CrownSpawnManager] Crown prefab needs DataCrown.");

            runner.Despawn(crownObject);
            return false;
        }

        dataCrown.InitializeTelegraph(telegraphDuration);

        Debug.Log(
            $"[CrownSpawnManager] Crown spawned at " +
            $"{selectedPoint.name}. Telegraph: {telegraphDuration:F1}s.");

        return true;
    }

    private static bool HasSpawnAuthority(NetworkRunner runner)
    {
        // This project uses Fusion Shared Mode.
        if (runner.GameMode == GameMode.Shared)
            return runner.IsSharedModeMasterClient;

        return runner.IsServer;
    }
}