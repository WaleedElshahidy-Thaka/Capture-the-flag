using Fusion;
using UnityEngine;

// Oil Slick (FD-04): drops an Oil surface patch behind the car. Environmental, not aimed - the
// patch is visible to everyone, and anyone (the dropper included) can drive onto it or around it.
// Same shape as DamperField / GrappleField: a car component PowerUpInventory calls on the host.
//
// Placement is a pure function of this car's pose on the activation tick and static arena geometry,
// so it needs no randomness.
[RequireComponent(typeof(NetworkObject))]
public sealed class OilSlick : NetworkBehaviour
{
    [Header("Patch")]
    [Tooltip("Prefab with NetworkObject + OilPatch. Built by Game/Build Oil Patch Prefab.")]
    [SerializeField] private NetworkObject patchPrefab;

    [Tooltip("oil_patch_lifetime (FD-04): provisional 8 s, range 5 to 15.")]
    [SerializeField, Range(5f, 15f)] private float lifetimeSeconds = 8f;

    [Tooltip("Not in FD-04 - provisional, confirm with design.")]
    [SerializeField, Min(0.5f)] private float radius = 2.5f;

    [Tooltip("Distance behind the chassis centre where the patch centre lands. Not in FD-04 - provisional.")]
    [SerializeField, Min(0f)] private float dropDistanceBehind = 2f;

    [Tooltip("Patches alive at once across the arena; the oldest goes first. 0 = no cap. Not in FD-04 - provisional.")]
    [SerializeField, Min(0)] private int maxActivePatches = 6;

    [Header("Placement")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.1f)] private float rayStartHeight = 2f;
    [SerializeField, Min(0.1f)] private float rayLength = 20f;

    [Header("Debug")]
    [SerializeField] private bool logOil = true;

    readonly RaycastHit[] hits = new RaycastHit[8];

    public bool TryActivate()
    {
        if (Object == null || !Object.HasStateAuthority)
            return false;

        if (patchPrefab == null)
        {
            Debug.LogError("[OilSlick] Patch prefab is not assigned.", this);
            return false; // the item stays in the slot
        }

        // Flattened heading, so a pitched chassis (ramp, landing) still drops straight behind.
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 target = transform.position - forward * dropDistanceBehind;

        // Airborne or over a ramp edge: the patch lands on whatever floor is below. The arena is
        // closed (FD-06), so a miss shouldn't happen - fall back to the target point.
        Vector3 center = target;
        Vector3 normal = Vector3.up;
        if (TryFindGround(target, out RaycastHit ground))
        {
            center = ground.point;
            normal = ground.normal;
        }

        Vector3 patchForward = Vector3.ProjectOnPlane(forward, normal);
        if (patchForward.sqrMagnitude < 0.0001f) patchForward = forward;
        patchForward.Normalize();

        RemoveOldestIfAtCap();

        int lifetimeTicks = Mathf.CeilToInt(lifetimeSeconds * Runner.TickRate);
        float patchRadius = radius;
        NetworkId dropper = Object.Id;

        NetworkObject patch = Runner.Spawn(
            patchPrefab,
            center,
            Quaternion.LookRotation(patchForward, normal),
            onBeforeSpawned: (runner, obj) =>
                obj.GetComponent<OilPatch>().Init(center, normal, patchForward, patchRadius, lifetimeTicks, dropper));

        if (patch == null)
        {
            Debug.LogError("[OilSlick] Failed to spawn oil patch.", this);
            return false;
        }

        if (logOil)
            Debug.Log($"[OilSlick] {name} dropped oil at {center} (r={patchRadius:F1} m, {lifetimeSeconds:F1} s).");

        return true;
    }

    // Nearest static hit below the target. Cars (and anything else with a Rigidbody) are skipped,
    // the same rule WallContact uses, so the patch never lands on the dropper or a car behind it.
    bool TryFindGround(Vector3 target, out RaycastHit ground)
    {
        ground = default;
        Vector3 origin = target + Vector3.up * rayStartHeight;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, rayLength, groundMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.attachedRigidbody != null) continue;
            if (hits[i].distance >= nearest) continue;
            nearest = hits[i].distance;
            ground = hits[i];
            found = true;
        }
        return found;
    }

    void RemoveOldestIfAtCap()
    {
        if (maxActivePatches <= 0) return;
        OilPatch oldest = OilSurfaceRegistry.FindOldest(Runner, out int count);
        if (oldest != null && count >= maxActivePatches)
            Runner.Despawn(oldest.Object);
    }
}
