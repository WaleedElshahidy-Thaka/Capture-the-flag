
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PowerUpCrate : NetworkBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float armDelaySeconds = 1f;

    [SerializeField, Min(0f)]
    private float respawnSeconds = 12f;

    [Header("References")]
    [SerializeField]
    private Collider pickupTrigger;

    [SerializeField]
    private GameObject crateVisual;

    [Networked]
    private TickTimer ArmTimer { get; set; }

    [Networked]
    private TickTimer RespawnTimer { get; set; }

    [Networked]
    public NetworkBool IsAvailable { get; private set; }

    [Networked]
    private NetworkBool IsVisible { get; set; }

    [Networked]
    private NetworkBool PickupPending { get; set; }

    [Networked]
    private NetworkId PendingCarId { get; set; }
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            IsAvailable = false;
            IsVisible = true;
            PickupPending = false;
            PendingCarId = default;
            ArmTimer = TickTimer.CreateFromSeconds(
                Runner, armDelaySeconds);

            RespawnTimer = TickTimer.None;
        }

        RefreshVisuals();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        // The crate is visible but cannot be collected yet.
        if (IsVisible && !IsAvailable)
        {
            if (ArmTimer.Expired(Runner))
            {
                ArmTimer = TickTimer.None;
                IsAvailable = true;

                Debug.Log("[PowerUpCrate] Ready for pickup.");
            }

            return;
        }

        // Respawn the crate after its cooldown.
        if (!IsVisible && RespawnTimer.Expired(Runner))
        {
            RespawnTimer = TickTimer.None;

            IsVisible = true;
            IsAvailable = false;

            ArmTimer = TickTimer.CreateFromSeconds(
                Runner, armDelaySeconds);

            Debug.Log("[PowerUpCrate] Respawned.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            !IsAvailable)
        {
            return;
        }

        PlayerMatchState player =
            other.GetComponentInParent<PlayerMatchState>();

        if (player == null || player.Object == null)
            return;

        NetworkObject carObject = player.Object;

        PowerUpInventory inventory =
            carObject.GetComponent<PowerUpInventory>();

        if (inventory == null || inventory.HasItem)
            return;

        bool isCrownHolder =
            CrownMatchState.Instance != null &&
            CrownMatchState.Instance.IsHolder(carObject);

        // Temporary default until your match state exposes a trailing-player
        // calculation. Do not enable catch-up weighting without that data.
        bool isTrailing = false;

        PowerUpType item = PowerUpRoller.Roll(
            Runner.Tick,
            player.Object.InputAuthority,
            isCrownHolder,
            isTrailing);

        TryBeginPickup(carObject, item);
    }

    public override void Render()
    {
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (crateVisual != null &&
            crateVisual.activeSelf != IsVisible)
        {
            crateVisual.SetActive(IsVisible);
        }

        if (pickupTrigger != null &&
            pickupTrigger.enabled != IsAvailable)
        {
            pickupTrigger.enabled = IsAvailable;
        }
    }

    public bool TryBeginPickup(
    NetworkObject carObject,
    PowerUpType item)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            !IsAvailable ||
            PickupPending ||
            carObject == null ||
            item == PowerUpType.None)
        {
            return false;
        }

        PowerUpPickupReceiver receiver =
            carObject.GetComponent<PowerUpPickupReceiver>();

        if (receiver == null)
        {
            Debug.LogError(
                "[PowerUpCrate] Car is missing PowerUpPickupReceiver.");
            return false;
        }

        // Reserve this crate so another car cannot claim it
        // while the first pickup request is being processed.
        PickupPending = true;
        PendingCarId = carObject.Id;

        receiver.RPC_RequestStoreItem(Object.Id, item);

        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ResolvePickup(
    NetworkId carId,
    NetworkBool stored)
    {
        if (!Object.HasStateAuthority ||
            !PickupPending ||
            carId != PendingCarId)
        {
            return;
        }

        PickupPending = false;
        PendingCarId = default;

        if (!stored)
        {
            // Inventory was full or storage failed.
            // Keep the crate available.
            Debug.Log("[PowerUpCrate] Pickup rejected; crate remains available.");
            return;
        }

        // Only successful storage consumes the crate.
        IsAvailable = false;
        IsVisible = false;

        ArmTimer = TickTimer.None;
        RespawnTimer = TickTimer.CreateFromSeconds(
            Runner, respawnSeconds);

        Debug.Log("[PowerUpCrate] Item stored; crate will respawn.");
    }
}
