using Fusion;
using System.Data;
using UnityEngine;

public class CrownSpawnManager : MonoBehaviour
{
    private HandlingValues handlingValues;

    [Header("Authored Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Crown Prefab")]
    [SerializeField] private NetworkObject crownPrefab;

    [Header("Spawn Rules")]
    [SerializeField, Range(0.55f, 0.85f)]
    private float spawnAsymmetryRatio = 0.7f;

    [SerializeField, Min(0f)]
    private float telegraphDuration = 3f;

    private void Awake()
    {
        handlingValues = HandlingValuesProvider.Current;
    }

    public bool SpawnCrown(
        NetworkRunner runner,
        CrownMatchState matchState)
    {
        Debug.Log($"[CrownSpawnManager] runner null: {runner == null}");
        Debug.Log($"[CrownSpawnManager] matchState null: {matchState == null}");
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
            handlingValues.minSpawnDistance,
            spawnAsymmetryRatio);

        if (selectedPoint == null)
        {
            Debug.LogError(
                "[CrownSpawnManager] No valid Crown spawn point found.");
            return false;
        }

        Vector3 newPosition = new Vector3( selectedPoint.position.x, 1 ,selectedPoint.position.y);//the corwn should spawn at a height of 1 unit above the ground

        NetworkObject crownObject = runner.Spawn(
            crownPrefab,
            newPosition,
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