using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PowerUpInventory))]
public class PowerUpPickupReceiver : NetworkBehaviour
{
    [SerializeField] private PowerUpInventory inventory;

    public override void Spawned()
    {
        if(inventory==null)
            inventory = GetComponent<PowerUpInventory>();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestStoreItem(
        NetworkId crateId,
        PowerUpType item)
    {
        if (!Object.HasStateAuthority)
            return;

        if (inventory == null)
            inventory = GetComponent<PowerUpInventory>();

        bool stored =
            inventory != null &&
            inventory.TryStoreItem(item);

        if (!Runner.TryFindObject(
                crateId, out NetworkObject crateObject))
        {
            return;
        }

        PowerUpCrate crate =
            crateObject.GetComponent<PowerUpCrate>();

        if (crate != null)
            crate.RPC_ResolvePickup(Object.Id, stored);
    }
}