using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class GrappleField : NetworkBehaviour
{
    // Tuning lives in HandlingValues (handling_values.json), next to the other Crown and contact
    // rules. Read live each time, so a changed JSON applies on the next match. The JSON skips the
    // [Range]/[Min] attributes, so values are clamped here to the FD-04 ranges.
    private static HandlingValues Values => HandlingValuesProvider.Current;

    private static float DurationSeconds => 
        Values.grappleDurationSeconds;

    private static float ReachBonus =>
        Values.grappleReachBonus;

    private static float MinimumRelativeSpeed =>
        Values.grappleMinimumRelativeSpeed;

    [Header("Crown Holder")]
    [Tooltip("If the Crown holder activates Grapple, it runs this long and then ends (it can't steal from itself).")]
    [SerializeField, Min(0.1f)]
    private float holderActiveSeconds = 1f;

    [Header("Debug")]
    [SerializeField]
    private bool logGrapple = true;

    private BoxCollider bodyCollider;
    private Rigidbody body;
    private NetworkObject networkObject;
    private PlayerMovement movement;

    [Networked]
    public NetworkBool IsActive { get; private set; }

    [Networked]
    private TickTimer ActiveTimer { get; set; }

    // True when the Crown holder activated Grapple: it runs holderActiveSeconds and is not ended by holding.
    [Networked]
    private NetworkBool StartedAsHolder { get; set; }

    public float GrappleReachBonus => ReachBonus;
    public BoxCollider BodyCollider => bodyCollider;

    /// <summary>
    /// Radius of the steal circle around this car: body half-length + Grapple reach bonus.
    /// The single source of truth for both the steal check and GrappleFieldRingFX.
    /// </summary>
    public float StealRadius
    {
        get
        {
            if (bodyCollider == null)
                return ReachBonus;

            Vector3 scale = bodyCollider.transform.lossyScale;
            float halfLength = Mathf.Max(bodyCollider.size.x * scale.x, bodyCollider.size.z * scale.z) * 0.5f;
            return halfLength + ReachBonus;
        }
    }

    /// <summary>
    /// True if any part of the holder's body is inside the steal circle (measured flat, on X/Z).
    /// </summary>
    public bool IsInStealRange(BoxCollider holder)
    {
        if (bodyCollider == null || holder == null)
            return false;

        Vector3 centre = bodyCollider.bounds.center;
        Vector3 offset = holder.ClosestPoint(centre) - centre;
        offset.y = 0f;

        float radius = StealRadius;
        return offset.sqrMagnitude <= radius * radius;
    }

    /// <summary>
    /// Estimated gap between the closest surface points of two body colliders (not their centres).
    /// </summary>
    public static float SurfaceGap(BoxCollider chaser, BoxCollider holder)
    {
        Vector3 pointOnHolder = holder.ClosestPoint(chaser.bounds.center);
        Vector3 pointOnChaser = chaser.ClosestPoint(pointOnHolder);
        pointOnHolder = holder.ClosestPoint(pointOnChaser);
        return Vector3.Distance(pointOnChaser, pointOnHolder);
    }

    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();

        // The car body uses a BoxCollider; the four tires use SphereColliders.
        Collider[] colliders = GetComponents<Collider>();

        foreach (Collider col in colliders)
        {
            if (col is BoxCollider box)
            {
                bodyCollider = box;
                break;
            }
        }

        if (bodyCollider == null)
        {
            Debug.LogError(
                $"[GrappleField] No body BoxCollider found on {name}.",
                this);
        }
    }

    public bool TryActivate()
    {
        if (Object == null || !Object.HasStateAuthority)
            return false;

        if (IsActive)
            return false;

        CrownMatchState crown = CrownMatchState.Instance;

        // Only a chaser can activate Grapple Field.
        if (crown == null /*|| crown.IsHolder(Object)*/) // uncomment this line to restrict to non-Crown holders only
            return false;

        // The holder can't steal from itself, so its Grapple only shows briefly and ends.
        StartedAsHolder = crown.IsHolder(Object);
        float seconds = StartedAsHolder ? holderActiveSeconds : DurationSeconds;

        IsActive = true;
        ActiveTimer = TickTimer.CreateFromSeconds(Runner, seconds);

        if (logGrapple)
        {
            Debug.Log(
                $"[GrappleField] Activated on {name} " +
                $"for {seconds:F1} seconds{(StartedAsHolder ? " (Crown holder)" : "")}.");
        }

        return true;
    }

    private void Deactivate(string reason)
    {
        IsActive = false;
        ActiveTimer = TickTimer.None;
        StartedAsHolder = false;

        if (logGrapple)
            Debug.Log($"[GrappleField] Ended on {name}: {reason}.");
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!IsActive)
            return;

        if (ActiveTimer.Expired(Runner))
        {
            Deactivate("expired");
            return;
        }

        CrownMatchState crown = CrownMatchState.Instance;

        // Captured the Crown (by Grapple or any other way) -> the power is spent.
        if (!StartedAsHolder && crown != null && crown.IsHolder(networkObject))
        {
            Deactivate("captured the Crown");
            return;
        }

        if (CheckForNearMissSteal())
            Deactivate("captured the Crown");
    }

    /// <summary>Returns true if this call stole the Crown.</summary>
    private bool CheckForNearMissSteal()
    {
        if (bodyCollider == null || body == null)
            return false;

        CrownMatchState crown = CrownMatchState.Instance;

        if (crown == null ||
            crown.Object == null ||
            !crown.Object.HasStateAuthority)
        {
            return false;
        }

        // The effect cannot steal if its owner already holds the Crown.
        if (crown.IsHolder(networkObject))
            return false;

        if (!crown.HasHolder)
            return false;

        // Resolve the current holder from the replicated NetworkId.
        if (!Runner.TryFindObject(crown.HolderId, out NetworkObject holderObject))
            return false;

        if (holderObject == null || holderObject == networkObject)
            return false;

        if (!holderObject.HasStateAuthority)
            return false;

        BoxCollider holderCollider =
            holderObject.GetComponent<BoxCollider>();

        Rigidbody holderBody =
            holderObject.GetComponent<Rigidbody>();

        if (holderCollider == null ||
            holderBody == null ||
            !holderCollider.enabled ||
            !bodyCollider.enabled)
        {
            return false;
        }

        // Steal area: a circle around this car (same radius the ring shows).
        if (!IsInStealRange(holderCollider))
            return false;

        // Physical contact is handled by CarContactClassifier.
        float surfaceDistance = SurfaceGap(bodyCollider, holderCollider);

        if (surfaceDistance <= 0.01f)
            return false;

        // A stationary car sitting nearby should not steal the Crown.
        float relativeSpeed =
            (body.linearVelocity - holderBody.linearVelocity).magnitude;

        if (relativeSpeed < MinimumRelativeSpeed)
            return false;

        // A Repulse shove never moves the Crown (FD-04), near-miss included.
        PlayerMovement holderMovement = holderObject.GetComponent<PlayerMovement>();

        if ((movement != null && movement.IsBeingShoved) ||
            (holderMovement != null && holderMovement.IsBeingShoved))
        {
            return false;
        }

        // Preserve Damper Field's protection behavior.
        DamperField damper = holderObject.GetComponent<DamperField>();

        if (damper != null && damper.IsActive)
        {
            if (logGrapple)
            {
                Debug.Log(
                    $"[GrappleField] Near-miss blocked: " +
                    $"{holderObject.name} is protected by Damper Field.");
            }

            return false;
        }

        // TryTransfer checks Crown protection and resets protection
        // for the new holder when a transfer succeeds.
        bool transferred = crown.TryTransfer(networkObject);

        if (logGrapple)
        {
            Debug.Log(
                transferred
                    ? $"[GrappleField] Near-miss steal succeeded: {name} " +
                      $"stole the Crown from {holderObject.name}. " +
                      $"Distance={surfaceDistance:F2}, " +
                      $"relative speed={relativeSpeed:F2}."
                    : $"[GrappleField] Near-miss transfer rejected " +
                      $"for {name}. Crown may be protected.");
        }

        return transferred;
    }
}