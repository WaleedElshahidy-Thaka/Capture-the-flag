using UnityEngine;

// Local-only, not networked. Three views:
//
//   Solo   - before a match is found: the same isometric angle as the lobby shot, framed on
//            your own robot alone (the LobbyPreviewCar).
//   Lobby  - one fixed isometric shot of the whole seat row, identical for every player
//            (LobbyLayout.CameraPosition/Rotation) - not relative to your own car.
//   Follow - third-person chase. Follows velocity direction rather than the car's facing (keeps
//            the view stable through turns instead of swinging with the nose), with reversing
//            unconditionally excluded from that so the camera never swings in front of the car
//            while backing up.
//
// Solo <-> Lobby goes through a fade to black (ScreenFade), which also hides the swap between
// the preview robot and the networked cars underneath it. The switch to Follow is driven by
// PlayerMatchState.CanMove, which the host releases on match start - no explicit "start the
// game camera" call needed anywhere.
//
// Position and facing come from the car's View child - the transform NetworkRigidbody
// interpolates between ticks - not from the physics root, which only moves at tick rate and
// would make the camera step at 60Hz under a higher frame rate. Velocity still comes from the
// Rigidbody, since the View has none.
public class PlayerCamera : MonoBehaviour
{
    enum Mode { Solo, Lobby, Follow, Hold }

    [SerializeField] MatchmakingFlowController flow;
    [SerializeField] Transform previewCar;
    [SerializeField] ScreenFade fade;

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
    Mode mode = Mode.Solo;
    bool fading;

    // Set at runtime by PlayerMovement - the car it should follow no longer exists at edit
    // time now that each player's car is spawned dynamically, not scene-placed.
    public void SetTarget(Rigidbody newTarget, Transform newView, PlayerMatchState newMatchState)
    {
        target = newTarget;
        view = newView != null ? newView : newTarget.transform;
        matchState = newMatchState;
        initialized = false;
    }

    bool InMatch => target != null && matchState != null && matchState.CanMove;

    void LateUpdate()
    {
        Mode desired = DesiredMode();

        // The only cut that fades is between the two lobby-side views; losing or gaining the
        // car (match start, host migration) switches at once.
        bool lobbySideCut = (mode == Mode.Solo && desired == Mode.Lobby) || (mode == Mode.Lobby && desired == Mode.Solo);
        if (desired != mode)
        {
            if (lobbySideCut && fade != null)
            {
                if (!fading)
                {
                    fading = true;
                    fade.Run(atBlack: () => mode = desired, onDone: () => fading = false);
                }
            }
            else
            {
                mode = desired;
            }
        }

        switch (mode)
        {
            case Mode.Follow: FollowView(); break;
            case Mode.Solo: SoloView(); break;
            case Mode.Lobby: LobbyView(); break;
            case Mode.Hold: break; // the car vanished mid-match (host migration) - keep the last view
        }
    }

    Mode DesiredMode()
    {
        if (InMatch) return Mode.Follow;
        if (target == null && mode == Mode.Follow) return Mode.Hold;
        if (mode == Mode.Hold && target == null) return Mode.Hold;
        bool solo = flow == null || flow.IsSoloView;
        return solo ? Mode.Solo : Mode.Lobby;
    }

    void SoloView()
    {
        Vector3 focus = previewCar != null ? previewCar.position : LobbyLayout.SlotPosition(0);
        transform.position = LobbyLayout.SoloCameraPosition(focus);
        transform.rotation = LobbyLayout.SoloCameraRotation(focus);
        initialized = false;
    }

    // The lobby shot needs no target at all, so it shows from the first frame and is the same
    // picture on every peer.
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
