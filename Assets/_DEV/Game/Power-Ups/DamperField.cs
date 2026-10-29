using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DamperField : NetworkBehaviour
{
    // Tuning lives in HandlingValues (handling_values.json), next to Grapple Field and the Crown
    // rules. Read live, so a changed JSON applies on the next activation. The JSON skips [Range],
    // so the value is clamped here to the FD-04 range.
    private static float DurationSeconds =>
        Mathf.Clamp(HandlingValuesProvider.Current.damperDurationSeconds, 3f, 7f);

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
        ActiveTimer = TickTimer.CreateFromSeconds(Runner, DurationSeconds);

        Debug.Log($"[DamperField] Activated on {name} for {DurationSeconds:F1}s.");
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