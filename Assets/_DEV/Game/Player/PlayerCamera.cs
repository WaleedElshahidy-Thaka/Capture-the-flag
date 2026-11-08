using Fusion.Addons.Physics;
using UnityEngine;

// Local-only camera with three views:
//   Solo   - before a match is found: framed on your preview robot (LobbyPreviewCar).
//   Lobby  - one fixed shot of the whole seat row, identical for every player (LobbyLayout).
//   Follow - in a match: third-person chase that follows the velocity direction (stable through
//            turns) except while reversing.
// Solo <-> Lobby cuts through a ScreenFade. Follow starts when the host releases CanMove.
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

    [Header("Spin-out")]
    [Tooltip("After a spin-out ends, the camera eases back behind the car this slowly (lower = gentler) " +
             "instead of whipping round.")]
    [SerializeField] float postSpinFollowSpring = 3f;
    [Tooltip("How long the gentle catch-up lasts after a spin-out, in seconds.")]
    [SerializeField] float postSpinSeconds = 0.6f;

    Vector3 followDirection = Vector3.forward;
    bool initialized;
    Mode mode = Mode.Solo;
    bool fading;
    PlayerMatchState boundPlayer;
    PlayerMovement movement;     // our car, for its spin-out state
    float spinEndedAt = -999f;
    bool wasSpinning;

    bool InMatch => target != null && matchState != null && matchState.CanMove;

    // view: the interpolated render child (NetworkRigidbody's target), so the camera moves smoothly
    // between network ticks. Velocity still comes from the Rigidbody.
    public void SetTarget(Rigidbody newTarget, Transform newView, PlayerMatchState newMatchState)
    {
        target = newTarget;
        view = newView != null ? newView : newTarget.transform;
        matchState = newMatchState;
        movement = newMatchState != null ? newMatchState.GetComponent<PlayerMovement>() : null;
        initialized = false;
    }

    void LateUpdate()
    {
        BindToLocalPlayer();
        UpdateMode();

        switch (mode)
        {
            case Mode.Follow: FollowView(); break;
            case Mode.Solo: SoloView(); break;
            case Mode.Lobby: LobbyView(); break;
            case Mode.Hold: break; // our car vanished mid-match (host migration): keep the last view
        }
    }

    // Our car is spawned at runtime and can be replaced after a host migration, so follow
    // whichever car this peer currently owns.
    void BindToLocalPlayer()
    {
        var local = PlayerRoster.Local;
        if (local == null || local == boundPlayer) return;
        boundPlayer = local;

        var networkRigidbody = local.GetComponent<NetworkRigidbody>();
        SetTarget(local.GetComponent<Rigidbody>(), networkRigidbody != null ? networkRigidbody.InterpolationTarget : null, local);
    }

    void UpdateMode()
    {
        Mode desired = DesiredMode();
        if (desired == mode) return;

        // The Solo <-> Lobby cut fades, and so does coming back from a finished match (Play Again):
        // the match view fades to black and opens on your preview robot. Gaining the car at match
        // start switches at once.
        bool lobbySideCut = (mode == Mode.Solo && desired == Mode.Lobby) || (mode == Mode.Lobby && desired == Mode.Solo);
        bool backToLobby = (mode == Mode.Hold || mode == Mode.Follow) && (desired == Mode.Solo || desired == Mode.Lobby);
        lobbySideCut |= backToLobby;
        if (!lobbySideCut || fade == null)
        {
            mode = desired;
            return;
        }

        if (fading) return;
        fading = true;
        fade.Run(atBlack: () => mode = desired, onDone: () => fading = false);
    }

    Mode DesiredMode()
    {
        if (InMatch) return Mode.Follow;

        // Our car vanished: hold the last view only while still in the match (a host migration is
        // about to give it back). Back in the lobby (Play Again), go to the lobby views - otherwise
        // the camera stayed frozen on the old match until the next car spawned.
        bool inLobby = flow != null && flow.Phase.IsPreMatch();
        if (!inLobby && target == null && (mode == Mode.Follow || mode == Mode.Hold)) return Mode.Hold;
        bool solo = flow == null || flow.IsSoloView;
        return solo ? Mode.Solo : Mode.Lobby;
    }

    void SoloView()
    {
        Vector3 focus = previewCar != null ? previewCar.position : LobbyLayout.SlotPosition(0);
        Quaternion facing = previewCar != null ? previewCar.rotation : LobbyLayout.SlotRotation(0);
        transform.position = LobbyLayout.SoloCameraPosition(focus, facing);
        transform.rotation = LobbyLayout.SoloCameraRotation(focus, facing);
        initialized = false;
    }

    void LobbyView()
    {
        transform.position = LobbyLayout.CameraPosition;
        transform.rotation = LobbyLayout.CameraRotation;

        // Seed the follow direction so the hand-off at match start swings in from behind.
        if (view != null) followDirection = view.forward;
        initialized = false;
    }

    void FollowView()
    {
        // Spin-out: the car whirls, the camera doesn't. It keeps looking the way it was looking, and
        // when the spin ends it eases back behind the car gently rather than snapping round.
        bool spinning = movement != null && movement.IsSpinningOut;
        if (wasSpinning && !spinning) spinEndedAt = Time.time;
        wasSpinning = spinning;

        Vector3 desired = spinning && initialized ? followDirection : DesiredFollowDirection();

        bool recovering = Time.time - spinEndedAt < postSpinSeconds;
        float turnSpring = recovering ? postSpinFollowSpring : followSpring;
        float turnAlpha = 1f - Mathf.Exp(-turnSpring * Time.deltaTime);
        float alpha = 1f - Mathf.Exp(-followSpring * Time.deltaTime); // position keeps up with the car as usual
        followDirection = initialized ? Vector3.Slerp(followDirection, desired, turnAlpha) : desired;
        initialized = true;

        Vector3 pivot = view.position + Vector3.up * height;
        Vector3 desiredPosition = pivot - followDirection * distance;
        Quaternion desiredRotation = Quaternion.LookRotation(followDirection, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, alpha);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, alpha);
    }

    // Behind the direction of travel; behind the nose when reversing or nearly stopped.
    Vector3 DesiredFollowDirection()
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
        return desired.normalized;
    }
}