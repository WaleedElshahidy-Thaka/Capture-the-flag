using Fusion;
using UnityEngine;

public class CrownMatchState : NetworkBehaviour
{
    public static CrownMatchState Instance { get; private set; }

    // NetworkId of the car that currently holds the Crown.
    [Networked]
    public NetworkId HolderId { get; set; }

    public override void Spawned()
    {
        if (Instance == null)
            Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsHolder(NetworkObject car)
    {
        return car != null &&
               HolderId != default &&
               HolderId == car.Id;
    }

    // Call only on State Authority.
    public void SetInitialHolder(NetworkObject car)
    {
        if (!Object.HasStateAuthority)
            return;

        if (car == null)
        {
            Debug.LogError("[Crown] Cannot assign Crown: car is null.");
            return;
        }

        HolderId = car.Id;
        Debug.Log($"[Crown] Initial holder: {car.name}");
    }

    // Call only on State Authority after contact classification succeeds.
    public bool TryTransfer(NetworkObject newHolder)
    {
        if (!Object.HasStateAuthority || newHolder == null)
            return false;

        if (HolderId == newHolder.Id)
            return false;

        HolderId = newHolder.Id;
        Debug.Log($"[Crown] Transferred to: {newHolder.name}");
        Debug.Log($"[Crown] Transfer executed at time {Time.time:F2}. " + $"New holder: {newHolder.Id}");
        return true;
    }
}