
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
    private GrappleField grapple;
    private OilSlick oilSlick;
    private RepulsePulse repulse;
    public override void Spawned()
    {
        movement = GetComponent<PlayerMovement>();
        damper = GetComponent<DamperField>();
        grapple = GetComponent<GrappleField>();
        oilSlick = GetComponent<OilSlick>();
        repulse = GetComponent<RepulsePulse>();
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
            case PowerUpType.GrappleField:
                {
                    if (grapple==null)
                    {
                        grapple = GetComponent<GrappleField>();
                    }
                    if (grapple == null)
                    {
                        Debug.LogError(
                            "[PowerUpInventory] GrappleField component is missing.");
                        return false;
                    }
                    if (!grapple.TryActivate())
                        return false;
                    HeldItem = PowerUpType.None;
                    Debug.Log("[PowerUpInventory] Grapple Field activated.");
                    return true;
                }
            case PowerUpType.OilSlick:
                {
                    if (oilSlick == null)
                        oilSlick = GetComponent<OilSlick>();

                    if (oilSlick == null)
                    {
                        Debug.LogError(
                            "[PowerUpInventory] OilSlick component is missing.");
                        return false;
                    }

                    // No holder/chaser condition: oil is environmental and always usable.
                    if (!oilSlick.TryActivate())
                        return false;

                    HeldItem = PowerUpType.None;
                    Debug.Log("[PowerUpInventory] Oil Slick activated.");
                    return true;
                }
            case PowerUpType.RepulsePulse:
                {
                    if (repulse == null)
                        repulse = GetComponent<RepulsePulse>();

                    if (repulse == null)
                    {
                        Debug.LogError(
                            "[PowerUpInventory] RepulsePulse component is missing.");
                        return false;
                    }

                    // Consumed on activation even if nobody ends up in range: committing the
                    // item is the decision (FD-04), so there is no refund.
                    if (!repulse.TryActivate())
                        return false;

                    HeldItem = PowerUpType.None;
                    Debug.Log("[PowerUpInventory] Repulse Pulse charging.");
                    return true;
                }
            // Add Repulse Pulse here when its effect is implemented.
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