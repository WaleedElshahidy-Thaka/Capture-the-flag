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

    Vector3 followDirection = Vector3.forward;
    bool initialized;
    Mode mode = Mode.Solo;
    bool fading;
    PlayerMatchState boundPlayer;

    bool InMatch => target != null && matchState != null && matchState.CanMove;

    // view: the interpolated render child (NetworkRigidbody's target), so the camera moves smoothly
    // between network ticks. Velocity still comes from the Rigidbody.
    public void SetTarget(Rigidbody newTarget, Transform newView, PlayerMatchState newMatchState)
    {
        target = newTarget;
        view = newView != null ? newView : newTarget.transform;
        matchState = newMatchState;
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

        // Only the Solo <-> Lobby cut fades; gaining or losing the car switches at once.
        bool lobbySideCut = (mode == Mode.Solo && desired == Mode.Lobby) || (mode == Mode.Lobby && desired == Mode.Solo);
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
        if (target == null && (mode == Mode.Follow || mode == Mode.Hold)) return Mode.Hold;
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
        Vector3 desired = DesiredFollowDirection();

        float alpha = 1f - Mathf.Exp(-followSpring * Time.deltaTime);
        followDirection = initialized ? Vector3.Slerp(followDirection, desired, alpha) : desired;
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
