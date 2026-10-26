
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PowerUpInventory : NetworkBehaviour
{
    // None means the slot is empty.
    [Networked]
    public PowerUpType HeldItem { get; private set; }

    public bool HasItem => HeldItem != PowerUpType.None;
    private PlayerMovement movement;
    private DamperField damper;
    public override void Spawned()
    {
        movement = GetComponent<PlayerMovement>();
        damper = GetComponent<DamperField>();
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

    // Activate the held item. Return false if it cannot be activated.
    public bool TryActivateHeldItem()
    {
        if (!Object.HasStateAuthority || !HasItem)
            return false;

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (movement == null)
        {
            Debug.LogError(
                "[PowerUpInventory] PlayerMovement is missing.");
            return false;
        }

        switch (HeldItem)
        {
            case PowerUpType.Overdrive:
                movement.GrantOverdrive();
                HeldItem = PowerUpType.None;

                Debug.Log("[PowerUpInventory] Overdrive activated.");
                return true;
            case PowerUpType.DamperField:
                {

                    if (damper == null)
                        damper = GetComponent<DamperField>();

                    if (damper == null)
                    {
                        Debug.LogError(
                            "[PowerUpInventory] DamperField component is missing.");
                        return false;
                    }

                    if (!damper.TryActivate())
                        return false;

                    HeldItem = PowerUpType.None;
                    Debug.Log("[PowerUpInventory] Damper Field activated.");
                    return true;
                }
            // Add Grapple Field, Oil Slick,
            // and Repulse Pulse here as their effects are implemented.
            default:
                Debug.Log(
                    $"[PowerUpInventory] {HeldItem} is not implemented yet.");
                return false;
        }
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
