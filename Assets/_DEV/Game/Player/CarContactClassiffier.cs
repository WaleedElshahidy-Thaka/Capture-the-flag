using Fusion;
using UnityEngine;

/// <summary>
/// Classifies car-to-car impacts.
/// Crown transfers are requested only by State Authority.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]

public sealed class CarContactClassifier : NetworkBehaviour
{
    [Header("Impact Classification")]
    [SerializeField, Min(0f)]
    private float minimumApproachSpeed = 1.5f;

    [SerializeField, Min(0f)]
    private float aggressorAdvantage = 1.0f;

    [SerializeField, Min(0f)]
    private float minimumContactNormalAlignment = 0.15f;

    [Header("Debug")]
    [SerializeField]
    private bool logContacts = true;

    private Rigidbody body;
    private NetworkObject networkObject;

    // Velocity at the end of this car's last network tick, i.e. BEFORE the physics step
    // that produces the collision. OnCollisionEnter sees post-impact velocities, so the
    // classifier uses this instead of body.linearVelocity.
    private Vector3 preStepVelocity;
    public Vector3 PreStepVelocity => preStepVelocity;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        networkObject = GetComponent<NetworkObject>();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
            preStepVelocity = body.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only the authoritative simulation decides gameplay outcomes.
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (collision == null || collision.contactCount == 0)
            return;

        NetworkObject otherObject =
            collision.collider.GetComponentInParent<NetworkObject>();

        if (otherObject == null || otherObject == networkObject)
            return;

        CarContactClassifier otherClassifier =
            otherObject.GetComponent<CarContactClassifier>();

        if (otherClassifier == null)
            return;

        // The Crown spawns when the match starts, after the cars exist, so the
        // reference is read at collision time and never cached.
        CrownMatchState crown = CrownMatchState.Instance;

        // Process only contacts where this car currently holds the Crown.
        if (crown == null || !crown.IsHolder(networkObject))
            return;

        Rigidbody otherBody = otherObject.GetComponent<Rigidbody>();
        if (otherBody == null)
            return;

        if (!TryClassifyAggressor(
                collision,
                otherBody,
                otherClassifier,
                out bool otherIsAggressor))
        {
            Debug.Log("[Crown] Ambiguous contact. Crown retained.");
            return;
        }

        if (otherIsAggressor)
            crown.TryTransfer(otherObject);
        else
            Debug.Log("[Crown] Crown holder was the clear aggressor; no transfer.");
    }

    private bool TryClassifyAggressor(
        Collision collision,
        Rigidbody otherBody,
        CarContactClassifier otherClassifier,
        out bool otherIsAggressor)
    {
        otherIsAggressor = false;

        ContactPoint contact = collision.GetContact(0);
        Vector3 normal = contact.normal.normalized;

        // Orient the normal so it points from this car toward the other.
        Vector3 toOther = otherBody.worldCenterOfMass - body.worldCenterOfMass;
        float alignment = Vector3.Dot(normal, toOther.normalized);

        if (Mathf.Abs(alignment) < minimumContactNormalAlignment)
        {
            if (logContacts)
                Debug.Log($"[Crown] Rejected: align={alignment:F2} (min {minimumContactNormalAlignment:F2})");
            return false;
        }

        if (alignment < 0f)
            normal = -normal;

        // Pre-impact velocities (see PreStepVelocity).
        Vector3 myVel = preStepVelocity;
        Vector3 otherVel = otherClassifier.PreStepVelocity;

        // Positive means the two cars are closing along the contact axis.
        float closingSpeed = Vector3.Dot(myVel - otherVel, normal);

        // How much each car contributes to closing the gap.
        float thisApproach = Vector3.Dot(myVel, normal);
        float otherApproach = -Vector3.Dot(otherVel, normal);

        if (logContacts)
            Debug.Log($"[Crown] closing={closingSpeed:F2} this={thisApproach:F2} other={otherApproach:F2} align={alignment:F2}");

        if (closingSpeed < minimumApproachSpeed)
            return false;

        // If neither car is clearly responsible, do not transfer.
        if (Mathf.Abs(otherApproach - thisApproach) < aggressorAdvantage)
            return false;

        otherIsAggressor = otherApproach > thisApproach;
        return true;
    }
}