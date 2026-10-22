
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PowerUpInventory : NetworkBehaviour
{
    // None means the slot is empty.
    [Networked]
    public PowerUpType HeldItem { get; private set; }

    public bool HasItem => HeldItem != PowerUpType.None;

    public override void Spawned()
    {
        // Initialise replicated state on the State Authority only.
        if (Object.HasStateAuthority)
        {
            HeldItem = PowerUpType.None;
        }
    }

    // Call from authority-side pickup logic.
    public bool TryStoreItem(PowerUpType item)
    {
        if (!Object.HasStateAuthority)
            return false;

        if (item == PowerUpType.None || HasItem)
            return false;

        HeldItem = item;
        return true;
    }

    // Call from authority-side activation logic after validating the effect.
    public bool TryConsumeItem(out PowerUpType item)
    {
        item = PowerUpType.None;

        if (!Object.HasStateAuthority || !HasItem)
            return false;

        item = HeldItem;
        HeldItem = PowerUpType.None;
        return true;
    }
}
