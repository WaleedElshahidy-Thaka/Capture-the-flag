using Fusion;
using UnityEngine;

public class DataCrown : NetworkBehaviour
{
    [Header("Visuals")]
    [SerializeField] private GameObject crownVisual;
    [SerializeField] private GameObject telegraphVisual;
    [SerializeField] private Collider pickupTrigger;

    [Networked]
    public NetworkBool IsTelegraphing { get; set; }

    [Networked]
    public NetworkBool IsPickupAvailable { get; set; }

    [Networked]
    private TickTimer TelegraphTimer { get; set; }

    private CrownMatchState matchState;

    public override void Spawned()
    {
        matchState = FindFirstObjectByType<CrownMatchState>();

        // The State Authority starts the replicated telegraph.
        if (Object.HasStateAuthority && !IsTelegraphing &&
            !IsPickupAvailable)
        {
            // Safety fallback if InitializeTelegraph was not called.
            InitializeTelegraph(3f);
        }

        RefreshVisuals();
    }

    public void InitializeTelegraph(float duration)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        IsTelegraphing = true;
        IsPickupAvailable = false;
        TelegraphTimer = TickTimer.CreateFromSeconds(Runner, duration);

        RefreshVisuals();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        var session = MatchmakingSessionState.Local;
        if (session != null && session.Frozen)
            return;

        if (IsTelegraphing && TelegraphTimer.Expired(Runner))
        {
            IsTelegraphing = false;
            IsPickupAvailable = true;

            Debug.Log("[Crown] Telegraph complete. Crown is available.");
        }
    }

    public override void Render()
    {
        if (matchState == null)
            matchState = FindFirstObjectByType<CrownMatchState>();

        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (crownVisual != null)
            crownVisual.SetActive(IsPickupAvailable);

        if (telegraphVisual != null)
            telegraphVisual.SetActive(IsTelegraphing);

        if (pickupTrigger != null)
            pickupTrigger.enabled =
                IsPickupAvailable &&
                matchState != null &&
                matchState.HasHolder == false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!IsPickupAvailable)
            return;

        if (matchState == null)
            matchState = FindFirstObjectByType<CrownMatchState>();

        if (matchState == null || matchState.HasHolder)
            return;

        PlayerMatchState player =
            other.GetComponentInParent<PlayerMatchState>();

        if (player == null || player.Object == null)
            return;

        if (matchState.TryPickup(player.Object))
        {
            IsPickupAvailable = false;

            Debug.Log(
                $"[Crown] Picked up by {player.name}.");

            RefreshVisuals();
        }
    }
}