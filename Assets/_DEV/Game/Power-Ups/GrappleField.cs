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

    public float GrappleReachBonus => ReachBonus;
    public BoxCollider BodyCollider => bodyCollider;

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
        if (crown == null || crown.IsHolder(Object))
            return false;

        IsActive = true;
        ActiveTimer = TickTimer.CreateFromSeconds(
            Runner, DurationSeconds);

        if (logGrapple)
        {
            Debug.Log(
                $"[GrappleField] Activated on {name} " +
                $"for {DurationSeconds:F1} seconds.");
        }

        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!IsActive)
            return;

        if (ActiveTimer.Expired(Runner))
        {
            IsActive = false;
            ActiveTimer = TickTimer.None;

            if (logGrapple)
                Debug.Log($"[GrappleField] Expired on {name}.");

            return;
        }

        CheckForNearMissSteal();
    }

    private void CheckForNearMissSteal()
    {
        if (bodyCollider == null || body == null)
            return;

        CrownMatchState crown = CrownMatchState.Instance;

        if (crown == null ||
            crown.Object == null ||
            !crown.Object.HasStateAuthority)
        {
            return;
        }

        // The effect cannot steal if its owner already holds the Crown.
        if (crown.IsHolder(networkObject))
            return;

        if (!crown.HasHolder)
            return;

        // Resolve the current holder from the replicated NetworkId.
        if (!Runner.TryFindObject(crown.HolderId, out NetworkObject holderObject))
            return;

        if (holderObject == null || holderObject == networkObject)
            return;

        if (!holderObject.HasStateAuthority)
            return;

        BoxCollider holderCollider =
            holderObject.GetComponent<BoxCollider>();

        Rigidbody holderBody =
            holderObject.GetComponent<Rigidbody>();

        if (holderCollider == null ||
            holderBody == null ||
            !holderCollider.enabled ||
            !bodyCollider.enabled)
        {
            return;
        }

        // Estimate the closest surface points between the body colliders.
        // This avoids measuring from the car centers.
        Vector3 pointOnHolder =
            holderCollider.ClosestPoint(bodyCollider.bounds.center);

        Vector3 pointOnChaser =
            bodyCollider.ClosestPoint(pointOnHolder);

        pointOnHolder =
            holderCollider.ClosestPoint(pointOnChaser);

        float surfaceDistance =
            Vector3.Distance(pointOnChaser, pointOnHolder);

        // Physical contact is handled by CarContactClassifier.
        // Grapple only handles a small gap between the body colliders.
        if (surfaceDistance <= 0.01f ||
            surfaceDistance > ReachBonus)
        {
            return;
        }

        // A stationary car sitting nearby should not steal the Crown.
        float relativeSpeed =
            (body.linearVelocity - holderBody.linearVelocity).magnitude;

        if (relativeSpeed < MinimumRelativeSpeed)
            return;

        // A Repulse shove never moves the Crown (FD-04), near-miss included.
        PlayerMovement holderMovement = holderObject.GetComponent<PlayerMovement>();

        if ((movement != null && movement.IsBeingShoved) ||
            (holderMovement != null && holderMovement.IsBeingShoved))
        {
            return;
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

            return;
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
    }
}