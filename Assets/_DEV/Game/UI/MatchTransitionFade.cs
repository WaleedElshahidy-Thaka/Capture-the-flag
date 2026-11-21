using UnityEngine;
using UnityEngine.UI;

// The match-start fade, exactly in this order:
//
//   1. lobby timer ends     -> a full black image FADES IN over the lobby
//   2. fully black          -> the camera cuts to your car at its start spot (hidden by the black)
//   3. black FADES OUT      -> you see your car in the arena
//   4. then the 3, 2, 1     -> the countdown waits for this (MatchTimerState's Intro Seconds)
//
// The countdown is held back on the host (MatchTimerState.introSeconds), so it starts after the fade on
// every machine. Keep Intro Seconds about Fade In + Hold + Fade Out here.
public class MatchTransitionFade : MonoBehaviour
{
    // True while a MatchTransitionFade is in the scene: PlayerCamera then leaves the start fade to it.
    public static bool InUse { get; private set; }
    // True while the screen is still going black: PlayerCamera keeps the lobby view until then.
    public static bool HoldingCamera { get; private set; }

    [Tooltip("A full-screen black Image, LAST in the Canvas so it covers everything. FadeScreen's Image works. " +
             "Raycast Target off.")]
    [SerializeField] Image blackImage;
    [SerializeField, Min(0.05f)] float fadeInSeconds = 0.4f;
    [Tooltip("Time held fully black while your car and the arena are set up.")]
    [SerializeField, Min(0f)] float holdSeconds = 0.3f;
    [SerializeField, Min(0.05f)] float fadeOutSeconds = 0.6f;

    enum State { Idle, FadingIn, Black, FadingOut }
    State state = State.Idle;
    float stateTime;
    bool wasStarting;

    void OnEnable() => InUse = true;

    void OnDisable()
    {
        InUse = false;
        HoldingCamera = false;
        SetAlpha(0f);
    }

    void Start() => SetAlpha(0f);

    void Update()
    {
        var session = MatchmakingSessionState.Local;
        bool starting = session != null && session.Object != null && session.Object.IsValid && session.MatchStarting;
        if (starting && !wasStarting && state == State.Idle) Enter(State.FadingIn);
        wasStarting = starting;

        stateTime += Time.unscaledDeltaTime;

        switch (state)
        {
            case State.FadingIn:
                HoldingCamera = true;
                SetAlpha(stateTime / fadeInSeconds);
                if (stateTime >= fadeInSeconds) Enter(State.Black);
                break;

            case State.Black:
                HoldingCamera = false; // the camera cuts to your car now, behind the black
                SetAlpha(1f);
                // Wait until the match exists and your car is released, plus the hold time.
                if (stateTime >= holdSeconds && (MatchReady() || !starting)) Enter(State.FadingOut);
                break;

            case State.FadingOut:
                SetAlpha(1f - stateTime / fadeOutSeconds);
                if (stateTime >= fadeOutSeconds) { SetAlpha(0f); Enter(State.Idle); }
                break;
        }
    }

    void Enter(State next)
    {
        state = next;
        stateTime = 0f;
        if (next != State.FadingIn) HoldingCamera = false;
    }

    // The match timer exists on this machine and your car is released into the match.
    static bool MatchReady()
    {
        var timer = MatchTimerState.Instance;
        var local = PlayerRoster.Local;
        return timer != null && timer.Object != null && timer.Object.IsValid &&
               local != null && local.HasState && local.CanMove;
    }

    void SetAlpha(float alpha)
    {
        if (blackImage == null) return;
        var c = blackImage.color;
        c.a = Mathf.Clamp01(alpha);
        blackImage.color = c;
        blackImage.raycastTarget = false;
    }
}
