using System;
using Fusion;
using UnityEngine;

// One Oil surface patch on the arena floor. Host-spawned, replicated simulation state (FD-05:
// "It changes handling, so it is simulation state").
//
// Placement lives in networked properties, not the Transform, so every peer runs Contains() on
// identical data without a NetworkTransform - and a host migration restores it from the snapshot
// (RestoreSnapshot copies state; Spawned re-applies the placement).
//
// Lifetime is AGE counted up by the host, like MatchTimerState's remaining ticks: a migration
// re-bases ticks, so an absolute TickTimer would expire at the wrong time on the new host. It also
// pauses while MatchmakingSessionState.Frozen is set.
[RequireComponent(typeof(NetworkObject))]
public sealed class OilPatch : NetworkBehaviour
{
    [Header("Visual (local only)")]
    [Tooltip("Flat disc, unit diameter. Must have NO collider - probes and the wall sensor would hit it.")]
    [SerializeField] private Transform visual;
    [Tooltip("Lift off the floor so the disc doesn't z-fight with it.")]
    [SerializeField, Min(0f)] private float surfaceOffset = 0.02f;
    [Tooltip("The patch shrinks away over its last seconds, so drivers can see it ending.")]
    [SerializeField, Min(0f)] private float shrinkSeconds = 1f;

    [Header("Audio")]
    [Tooltip("FD-04 activation cue, distinct per item. Optional.")]
    [SerializeField] private AudioSource dropAudio;

    [Header("Contact")]
    [Tooltip("How far a probe contact may sit off the patch plane and still count as on it.")]
    [SerializeField, Min(0f)] private float verticalTolerance = 0.5f;

    // Declaration order is the network layout - keep it stable (new fields go at the end).
    [Networked] public Vector3 Center { get; private set; }
    [Networked] public Vector3 Normal { get; private set; }
    [Networked] public Vector3 Forward { get; private set; }
    [Networked] public float Radius { get; private set; }
    [Networked] public int LifetimeTicks { get; private set; }
    [Networked] public int AgeTicks { get; private set; }
    [Networked] public NetworkId DropperId { get; private set; } // the car that dropped it, for the event feed

    // Same guard as PlayerMatchState: networked properties are unreadable outside Spawned/Despawned.
    public bool HasState { get; private set; }
    public bool IsActive => AgeTicks < LifetimeTicks;

    // Fires once per peer when a patch appears. Hook the event feed / audio here.
    public static event Action<OilPatch> Dropped;

    Quaternion inverseRotation = Quaternion.identity;

    // Host only, from Runner.Spawn's onBeforeSpawned.
    public void Init(Vector3 center, Vector3 normal, Vector3 forward, float radius, int lifetimeTicks, NetworkId dropperId)
    {
        Center = center;
        Normal = normal.normalized;
        Forward = forward.normalized;
        Radius = radius;
        LifetimeTicks = Mathf.Max(1, lifetimeTicks);
        AgeTicks = 0;
        DropperId = dropperId;
    }

    public override void Spawned()
    {
        HasState = true;

        Quaternion rotation = Quaternion.LookRotation(Forward, Normal);
        inverseRotation = Quaternion.Inverse(rotation);
        transform.SetPositionAndRotation(Center + Normal * surfaceOffset, rotation);
        ApplyScale(1f);

        OilSurfaceRegistry.Register(Runner, this);

        // Spawned also runs for a patch restored after a host migration; don't replay the drop then.
        bool freshDrop = AgeTicks * Runner.DeltaTime < 0.5f;
        if (freshDrop)
        {
            if (dropAudio != null) dropAudio.Play();
            Dropped?.Invoke(this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        HasState = false;
        OilSurfaceRegistry.Unregister(runner, this);
    }

    // Host only.
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        var session = MatchmakingSessionState.Local;
        if (session != null && session.Frozen) return;

        AgeTicks++;
        if (AgeTicks >= LifetimeTicks)
            Runner.Despawn(Object);
    }

    // True if a ground point lies on the patch: inside the disc and close to its plane.
    // Uses only networked values, so every peer answers the same.
    public bool Contains(Vector3 groundPoint)
    {
        Vector3 local = inverseRotation * (groundPoint - Center);
        if (Mathf.Abs(local.y) > verticalTolerance) return false;
        return local.x * local.x + local.z * local.z <= Radius * Radius;
    }

    public override void Render()
    {
        float secondsLeft = (LifetimeTicks - AgeTicks) * Runner.DeltaTime;
        float scale = shrinkSeconds > 0f ? Mathf.Clamp01(secondsLeft / shrinkSeconds) : 1f;
        ApplyScale(scale);
    }

    void ApplyScale(float scale)
    {
        if (visual == null) return;
        float diameter = Radius * 2f * scale;
        visual.localScale = new Vector3(diameter, visual.localScale.y, diameter);
    }
}
