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
    [Header("Debug")]
    [SerializeField]
    private bool logContacts = true;

    private Rigidbody body;
    private NetworkObject networkObject;
    private PlayerMovement movement;
    // Velocity at the end of this car's last network tick, i.e. BEFORE the physics step
    // that produces the collision. OnCollisionEnter sees post-impact velocities, so the
    // classifier uses this instead of body.linearVelocity.
    private Vector3 preStepVelocity;
    public Vector3 PreStepVelocity => preStepVelocity;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        networkObject = GetComponent<NetworkObject>();
        movement = GetComponent<PlayerMovement>();
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
        // Process each pair only once when both callbacks occur.
        if (networkObject.Id.Raw > otherObject.Id.Raw) // what is the purpose of this line??????
            return;


        CarContactClassifier otherClassifier =
            otherObject.GetComponent<CarContactClassifier>();

        if (otherClassifier == null ||
            !otherObject.HasStateAuthority)
            return;

        Rigidbody otherBody = otherObject.GetComponent<Rigidbody>();
        PlayerMovement otherMovement = otherObject.GetComponent<PlayerMovement>();


        if (otherBody == null || otherMovement == null || movement == null)
            return;

        if (!TryClassifyAggressor(
                collision,
                otherBody,
                otherClassifier,
                out bool otherIsAggressor))
        {
            if (logContacts)
                Debug.Log("[Contact] Ambiguous impact; no slam.");

            return;
        }

        CarContactClassifier aggressor =
           otherIsAggressor ? otherClassifier : this;

        CarContactClassifier victim =
            otherIsAggressor ? this : otherClassifier;

        PlayerMovement victimMovement =
            otherIsAggressor ? movement : otherMovement;

        // Do not repeatedly slam a car that is already spinning
        // or has contact immunity.
        if (!victimMovement.IsSpinningOut &&
            !victimMovement.HasContactImmunity &&
            aggressor.preStepVelocity.magnitude >= HandlingValuesProvider.Current.slamMinimumSpeed)
        {
            Vector3 travelDirection = victim.preStepVelocity;

            if (travelDirection.sqrMagnitude < 0.01f)
                travelDirection = victim.transform.forward;

            travelDirection.Normalize();

            Vector3 toAggressor =
                aggressor.transform.position - victim.transform.position;

            float cross = Vector3.Cross(
                victim.transform.forward,
                toAggressor.normalized).y;

            float spinSign = cross >= 0f ? 1f : -1f;

            victimMovement.EnterSpinout(
                travelDirection,
                spinSign);

            if (logContacts)
            {
                Debug.Log(
                    $"[Slam] {aggressor.name} slammed {victim.name}.");
            }
        }
        bool flowControl = HandleCrownTransferOnImpact(aggressor, victim);
        if (!flowControl)
        {
            return;
        }
    }

    private bool HandleCrownTransferOnImpact(CarContactClassifier aggressor, CarContactClassifier victim)
    {
        // The Crown spawns when the match starts, after the cars exist, so the
        // reference is read at collision time and never cached.
        CrownMatchState crown = CrownMatchState.Instance;

        if (crown == null)
            return false;

        // The Crown transfers only if its current holder is the victim.
        // The aggressor receives the Crown.
        if (crown.IsHolder(victim.networkObject))
        {
            bool transferred = crown.TryTransfer(aggressor.networkObject);

            if (logContacts)
            {
                Debug.Log(
                    transferred
                        ? $"[Crown] Transferred to aggressor: {aggressor.name}"
                        : "[Crown] Transfer blocked (possibly protected).");
            }
        }

        return true;
    }


    private bool TryClassifyAggressor(Collision collision,Rigidbody otherBody, CarContactClassifier otherClassifier, out bool otherIsAggressor)
    {
        otherIsAggressor = false;

        ContactPoint contact = collision.GetContact(0);
        Vector3 normal = contact.normal.normalized;

        // Orient the normal so it points from this car toward the other.
        Vector3 toOther = otherBody.worldCenterOfMass - body.worldCenterOfMass;

        if (toOther.sqrMagnitude < 0.0001f)
            return false;

        float alignment = Vector3.Dot(normal, toOther.normalized);

        if (Mathf.Abs(alignment) < HandlingValuesProvider.Current.minimumContactNormalAlignment)
        {
            if (logContacts)
                Debug.Log($"[Crown] Rejected: align={alignment:F2} (min {HandlingValuesProvider.Current.minimumContactNormalAlignment:F2})");
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
        {
            Debug.Log(
                $"[Contact] Closing={closingSpeed:F2}, " +
                $"This={thisApproach:F2}, " +
                $"Other={otherApproach:F2}, " +
                $"Alignment={alignment:F2}");
        }

        if (closingSpeed < HandlingValuesProvider.Current.minimumApproachSpeed)
            return false;

        // If neither car is clearly responsible, do not transfer.
        if (Mathf.Abs(otherApproach - thisApproach) < HandlingValuesProvider.Current.aggressorAdvantage)
            return false;

        otherIsAggressor = otherApproach > thisApproach;
        return true;
    }
}