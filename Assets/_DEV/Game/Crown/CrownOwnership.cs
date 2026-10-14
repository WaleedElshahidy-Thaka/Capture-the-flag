
using Fusion;
using UnityEngine;

/// <summary>
/// Stores Crown ownership on the network.
/// Attach to the GameObject containing MatchmakingSessionState
/// and NetworkObject.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class CrownOwnership : NetworkBehaviour
{
    [Networked]
    public NetworkId HolderId { get; private set; }

    public bool HasHolder => HolderId != default;

    public bool IsHeldBy(NetworkObject car)
    {
        return car != null &&
               HasHolder &&
               HolderId == car.Id;
    }

    /// <summary>
    /// Call on State Authority when a match selects its initial holder.
    /// </summary>
    public bool SetInitialHolder(NetworkObject car)
    {
        if (!Object.HasStateAuthority || car == null)
            return false;

        HolderId = car.Id;
        return true;
    }

    /// <summary>
    /// Only State Authority can transfer the Crown.
    /// </summary>
    public bool TryTransfer(NetworkObject newHolder)
    {
        if (!Object.HasStateAuthority || newHolder == null)
            return false;

        if (HolderId == newHolder.Id)
            return false;

        HolderId = newHolder.Id;

        Debug.Log($"[Crown] Ownership transferred to {newHolder.name}");
        return true;
    }

    /// <summary>
    /// Resolves the current holder on this peer.
    /// </summary>
    public NetworkObject GetCurrentHolder()
    {
        if (!HasHolder || Runner == null)
            return null;

        return Runner.TryFindObject(HolderId, out NetworkObject holder)
            ? holder
            : null;
    }
}
