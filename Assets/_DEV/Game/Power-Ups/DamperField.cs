
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DamperField : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField, Range(3f, 7f)]
    private float durationSeconds = 5f;

    [Header("Visuals")]
    [SerializeField]
    private GameObject ringVisual;

    [Networked]
    private TickTimer ActiveTimer { get; set; }

    [Networked]
    public NetworkBool IsActive { get; private set; }

    public bool TryActivate()
    {
        if (Object == null || !Object.HasStateAuthority)
            return false;

        // Damper Field is only useful for the Crown holder.
        CrownMatchState crown = CrownMatchState.Instance;

        if (crown == null || !crown.IsHolder(Object))
            return false;

        if (IsActive)
            return false;

        IsActive = true;
        ActiveTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);

        Debug.Log($"[DamperField] Activated on {name} for {durationSeconds:F1}s.");
        RefreshVisual();
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !IsActive)
            return;

        if (ActiveTimer.Expired(Runner))
        {
            IsActive = false;
            Debug.Log($"[DamperField] Expired on {name}.");
        }
    }

    public override void Render()
    {
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (ringVisual != null)
            ringVisual.SetActive(IsActive);
    }
}
