using System;
using Fusion;
using UnityEngine;

// Repulse Pulse (FD-04): a short-range, omnidirectional shove behind a visible charge-up.
//
// Not aimed: every car inside the radius gets the same push, straight away from the user, whoever
// it is. The charge-up is replicated and shows the pulse's reach, so everyone can see it coming
// and react - "the telegraph is what makes it fair".
//
// Physics only: a shoved car is flagged (PlayerMovement.IsBeingShoved) and contact code ignores
// contacts while the flag is up, so a shove never slams anyone and never moves the Crown. It also
// never touches steering - the shoved car is pushed, not driven.
//
// Same shape as DamperField: a car component PowerUpInventory calls on the host. Charge time is
// counted down by the host (migration-safe, pauses while frozen), like MatchTimerState.
[RequireComponent(typeof(NetworkObject))]
public sealed class RepulsePulse : NetworkBehaviour
{
    // Tuning lives in HandlingValues (handling_values.json), next to Grapple and Damper. Read live, so
    // a changed JSON applies on the next pulse. The JSON skips [Range]/[Min], so values are clamped here.
    static HandlingValues Values => HandlingValuesProvider.Current;
    static float ChargeSeconds => Mathf.Clamp(Values.repulseChargeSeconds, 0.3f, 1.5f);
    static float RadiusSetting => Mathf.Max(1f, Values.repulseRadius);
    static float ShoveSpeed => Mathf.Max(0f, Values.repulseShoveSpeed);
    static float ShoveSeconds => Mathf.Max(0f, Values.repulseShoveSeconds);
    static float MaxHeightDifference => Mathf.Max(0f, Values.repulseMaxHeightDifference);

    [Header("Visuals (local only) - unit-diameter spheres with no collider")]
    [Tooltip("Grows to the pulse's full reach over the charge, so the telegraph also shows the range.")]
    [SerializeField] private Transform chargeVisual;
    [SerializeField] private Transform burstVisual;
    [SerializeField, Min(0.05f)] private float burstSeconds = 0.35f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource chargeAudio;
    [SerializeField] private AudioSource pulseAudio;

    [Header("Debug")]
    [SerializeField] private bool logPulse = true;

    // Declaration order is the network layout - keep it stable (new fields go at the end).
    [Networked] public int ChargeTicksRemaining { get; private set; }
    [Networked] private int ChargeTicksTotal { get; set; }
    [Networked] public int PulseCount { get; private set; } // every peer plays the burst when this changes
    // The reach the host will use, captured at activation. Clients draw the telegraph from this, not
    // from their own handling_values.json, so the sphere they see always matches the real pulse.
    [Networked] private float PulseRadius { get; set; }

    public bool IsCharging => ChargeTicksRemaining > 0;
    public float Radius => PulseRadius > 0f ? PulseRadius : RadiusSetting;

    // Raised on every peer when the pulse fires (from Render). Hook the event feed here.
    public static event Action<RepulsePulse> Fired;

    int lastSeenPulseCount;
    bool wasCharging;
    float burstTimeLeft;

    public override void Spawned()
    {
        // A car restored after a host migration must not replay an old burst.
        lastSeenPulseCount = PulseCount;
        wasCharging = IsCharging;
        SetActive(chargeVisual, false);
        SetActive(burstVisual, false);
    }

    public bool TryActivate()
    {
        if (Object == null || !Object.HasStateAuthority)
            return false;

        if (IsCharging || MatchEnded())
            return false;

        PulseRadius = RadiusSetting;
        ChargeTicksTotal = Mathf.Max(1, Mathf.CeilToInt(ChargeSeconds * Runner.TickRate));
        ChargeTicksRemaining = ChargeTicksTotal;

        if (logPulse)
            Debug.Log($"[RepulsePulse] {name} charging ({ChargeSeconds:F2}s, r={PulseRadius:F1} m).");

        return true;
    }

    // Host only.
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !IsCharging)
            return;

        var session = MatchmakingSessionState.Local;
        if (session != null && session.Frozen)
            return;

        // Nothing lands after the buzzer (FD-01). The item is still spent.
        if (MatchEnded())
        {
            ChargeTicksRemaining = 0;
            return;
        }

        ChargeTicksRemaining--;
        if (ChargeTicksRemaining == 0)
            Fire();
    }

    static bool MatchEnded() =>
        MatchTimerState.Instance != null && MatchTimerState.Instance.Finished;

    void Fire()
    {
        PulseCount++;

        Vector3 origin = transform.position;
        float radiusSqr = PulseRadius * PulseRadius;
        int shoved = 0;

        // Roster order doesn't matter: every car gets the same push, computed from positions at
        // the start of this tick's loop, and nobody's push depends on anyone else's.
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
        {
            PlayerMatchState car = cars[i];
            if (car == null || !car.HasState || car.Object == Object)
                continue;

            Vector3 offset = car.transform.position - origin;
            if (Mathf.Abs(offset.y) > MaxHeightDifference)
                continue;

            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
                continue;

            // Two cars exactly stacked have no "away": use the user's right, which is deterministic.
            Vector3 direction = offset.sqrMagnitude > 0.0001f
                ? offset.normalized
                : Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            PlayerMovement movement = car.GetComponent<PlayerMovement>();
            if (movement == null)
                continue;

            movement.ApplyShove(direction * ShoveSpeed, ShoveSeconds);
            shoved++;
        }

        // Fires and is consumed even with nobody in range (FD-04: no refund).
        if (logPulse)
            Debug.Log($"[RepulsePulse] {name} fired: {shoved} car(s) shoved.");
    }

    public override void Render()
    {
        // Charge-up: grows to the full reach, so opponents read both "it's coming" and "how far".
        bool charging = IsCharging;
        if (charging != wasCharging && chargeAudio != null)
        {
            if (charging) chargeAudio.Play();
            else chargeAudio.Stop();
        }
        wasCharging = charging;

        SetActive(chargeVisual, charging);
        if (charging && chargeVisual != null && ChargeTicksTotal > 0)
        {
            float progress = 1f - (float)ChargeTicksRemaining / ChargeTicksTotal;
            SetDiameter(chargeVisual, Radius * 2f * Mathf.Lerp(0.15f, 1f, progress));
        }

        // Burst: one short expanding shell when PulseCount changes.
        if (PulseCount != lastSeenPulseCount)
        {
            lastSeenPulseCount = PulseCount;
            burstTimeLeft = burstSeconds;
            if (pulseAudio != null) pulseAudio.Play();
            Fired?.Invoke(this);
        }

        bool bursting = burstTimeLeft > 0f;
        SetActive(burstVisual, bursting);
        if (bursting && burstVisual != null)
        {
            float t = 1f - burstTimeLeft / burstSeconds;
            SetDiameter(burstVisual, Radius * 2f * Mathf.Lerp(0.85f, 1.15f, t));
            burstTimeLeft -= Time.deltaTime;
        }
    }

    static void SetActive(Transform t, bool active)
    {
        if (t != null && t.gameObject.activeSelf != active)
            t.gameObject.SetActive(active);
    }

    static void SetDiameter(Transform t, float diameter) =>
        t.localScale = new Vector3(diameter, diameter, diameter);
}