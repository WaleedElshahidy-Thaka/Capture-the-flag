using Fusion;
using UnityEngine;

public class CrownMatchState : NetworkBehaviour
{
    public static CrownMatchState Instance { get; private set; }

    // NetworkId of the car that currently holds the Crown.
    [Networked]
    public NetworkId HolderId { get; set; }

    [Header("Crown Protection")]
    [SerializeField, Min(0f)]
    private float protectionSeconds = 3f;

    [Networked]
    private int ProtectionTicksRemaining { get; set; }

    public bool IsProtected => ProtectionTicksRemaining > 0;
    public bool HasHolder => HolderId != default;
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
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        var session = MatchmakingSessionState.Local;
        if (session != null && session.Frozen)
            return;

        if (ProtectionTicksRemaining > 0)
        {
            ProtectionTicksRemaining--;
            
        }
         
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
        ResetProtectionTime();

        Debug.Log($"[Crown] Initial holder: {car.name}. " + $"Protected for {protectionSeconds:F1} seconds.");
    }
    public bool TryPickup(NetworkObject car)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            car == null ||
            HasHolder ||
            IsProtected)
        {
            return false;
        }

        HolderId = car.Id;
        ResetProtectionTime();

        Debug.Log($"[Crown] Picked up by {car.name}.");
        return true;
    }
    private void ResetProtectionTime()
    {
        ProtectionTicksRemaining = Mathf.CeilToInt(protectionSeconds * Runner.TickRate);
    }

    // Call only on State Authority after contact classification succeeds.
    public bool TryTransfer(NetworkObject newHolder)
    {
        if(!Object.HasStateAuthority || newHolder == null)
            return false;

        if (HolderId == newHolder.Id)
            return false;

        if (IsProtected)
        {
           Debug.Log($"Crown is protected: {IsProtected}");
            //Debug.Log($"[Crown] Transfer to {newHolder.name} blocked: current holder is protected for {ProtectionTicksRemaining / Runner.TickRate:F2} more seconds.");
            return false;
        }

        HolderId = newHolder.Id;
        ResetProtectionTime(); // Restart protection for the new holder.


        //Debug.Log($"[Crown] Transferred to: {newHolder.name}");
        //Debug.Log($"[Crown] Transfer executed at time {Time.time:F2}. " + $"New holder: {newHolder.Id}");
        return true;
        
    }

    //
}