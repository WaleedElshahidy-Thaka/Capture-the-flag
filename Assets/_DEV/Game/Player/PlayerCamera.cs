using UnityEngine;

// Local-only, not networked. Two views, picked by whether the match has started:
//
//   Lobby  - one fixed isometric shot of the whole seat row, identical for every player
//            (LobbyLayout.CameraPosition/Rotation) - not relative to your own car.
//   Follow - third-person chase. Follows velocity direction rather than the car's facing (keeps
//            the view stable through turns instead of swinging with the nose), with reversing
//            unconditionally excluded from that so the camera never swings in front of the car
//            while backing up.
//
// The switch is driven by PlayerMatchState.CanMove, which the host releases on match start -
// no explicit "start the game camera" call needed anywhere.
//
// Position and facing come from the car's View child - the transform NetworkRigidbody
// interpolates between ticks - not from the physics root, which only moves at tick rate and
// would make the camera step at 60Hz under a higher frame rate. Velocity still comes from the
// Rigidbody, since the View has none.
public class PlayerCamera : MonoBehaviour
{
    [SerializeField] Rigidbody target;
    [SerializeField] Transform view;
    [SerializeField] PlayerMatchState matchState;

    [Header("Follow view (match)")]
    [SerializeField] float distance = 6f;
    [SerializeField] float height = 2.5f;
    [SerializeField] float pitch = 12f; // degrees, downward
    [SerializeField] float followSpring = 10f;
    [SerializeField] float reverseFollowThreshold = 2f; // m/s

    Vector3 followDirection = Vector3.forward;
    bool initialized;

    // Set at runtime by PlayerMovement.Spawned() - the car it should follow no longer exists
    // at edit time now that each player's car is spawned dynamically, not scene-placed.
    public void SetTarget(Rigidbody newTarget, Transform newView, PlayerMatchState newMatchState)
    {
        target = newTarget;
        view = newView != null ? newView : newTarget.transform;
        matchState = newMatchState;
        initialized = false;
    }

    bool InMatch => target != null && matchState != null && matchState.CanMove;
    bool wasInMatch;

    // The lobby shot needs no target at all, so it shows from the first frame - before this
    // client's own car has even spawned - and is the same picture on every peer. Losing the
    // car mid-match (host migration destroys every object, then rebuilds them) holds the last
    // view rather than cutting to the lobby shot behind the "reconnecting" overlay.
    void LateUpdate()
    {
        if (InMatch)
        {
            FollowView();
            wasInMatch = true;
        }
        else if (target == null && wasInMatch)
        {
            // hold
        }
        else
        {
            LobbyView();
            wasInMatch = false;
        }
    }

    void LobbyView()
    {
        transform.position = LobbyLayout.CameraPosition;
        transform.rotation = LobbyLayout.CameraRotation;

        // Seed the follow view so the hand-off on match start swings smoothly from behind.
        if (view != null) followDirection = view.forward;
        initialized = false;
    }

    void FollowView()
    {
        Vector3 velocity = target.linearVelocity;
        Vector3 carForward = view.forward;
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

        Vector3 pivot = view.position + Vector3.up * height;
        Vector3 desiredPosition = pivot - followDirection * distance;
        Quaternion desiredRotation = Quaternion.LookRotation(followDirection, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, alpha);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, alpha);
    }
}
