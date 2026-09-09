using UnityEngine;

// Local-only, not networked. Follows velocity direction rather than the car's facing (keeps
// the view stable through turns instead of swinging with the nose), with reversing
// unconditionally excluded from that so the camera never swings in front of the car while
// backing up. Reads the target's real Rigidbody directly - no separate synced/visual object,
// same object PlayerMovement drives.
public class PlayerCamera : MonoBehaviour
{
    [SerializeField] Rigidbody target;

    [SerializeField] float distance = 6f;
    [SerializeField] float height = 2.5f;
    [SerializeField] float pitch = 12f; // degrees, downward
    [SerializeField] float followSpring = 10f;
    [SerializeField] float reverseFollowThreshold = 2f; // m/s

    Vector3 followDirection = Vector3.forward;
    bool initialized;

    // Set at runtime by PlayerMovement.Spawned() - the car it should follow no longer exists
    // at edit time now that each player's car is spawned dynamically, not scene-placed.
    public void SetTarget(Rigidbody newTarget) => target = newTarget;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 velocity = target.linearVelocity;
        Vector3 carForward = target.transform.forward;
        float forwardSpeed = Vector3.Dot(velocity, carForward);

        Vector3 desired;
        if (forwardSpeed < -reverseFollowThreshold)
            desired = carForward;
        else if (velocity.magnitude < reverseFollowThreshold)
            desired = initialized ? followDirection : carForward;
        else
            desired = velocity.normalized;

        desired.y = 0f;
        if (desired.sqrMagnitude < 0.0001f) desired = carForward;
        desired.Normalize();

        float alpha = 1f - Mathf.Exp(-followSpring * Time.deltaTime);
        followDirection = initialized ? Vector3.Slerp(followDirection, desired, alpha) : desired;
        initialized = true;

        Vector3 pivot = target.transform.position + Vector3.up * height;
        Vector3 desiredPosition = pivot - followDirection * distance;
        Quaternion desiredRotation = Quaternion.LookRotation(followDirection, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, alpha);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, alpha);
    }
}
