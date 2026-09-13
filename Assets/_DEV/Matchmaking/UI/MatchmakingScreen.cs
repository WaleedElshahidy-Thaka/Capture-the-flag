using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pure UI binding, no matchmaking logic of its own - mirrors the shelved WaitingRoomScreen's
// role. One screen, panels toggled per MatchmakingPhase (Connecting / Idle / Searching /
// WaitingSolo / WaitingReady / Starting).
public class MatchmakingScreen : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [SerializeField] GameObject idlePanel;
    [SerializeField] Button findMatchButton;
    [SerializeField] TMP_Text loadingText; // "Loading Game... {elapsed}s" - shown while Connecting, disables findMatchButton

    [SerializeField] GameObject searchingPanel;
    [SerializeField] TMP_Text searchingText; // "Finding player... {elapsed}s"

    [SerializeField] GameObject soloBotPanel;
    [SerializeField] Button startWithBotsButton;

    [SerializeField] GameObject readyPanel;
    [SerializeField] Button readyToggleButton;
    [SerializeField] TMP_Text readyToggleLabel;

    [SerializeField] GameObject startingPanel;
    [SerializeField] Button cancelButton;

    // Purely local wall-clock reference for the Connecting message - "how long has this client
    // been trying to connect" isn't networked state, so it doesn't belong on IActiveQuickMatchSession.
    float awakeRealtime;

    void Awake()
    {
        awakeRealtime = Time.realtimeSinceStartup;

        findMatchButton.onClick.AddListener(flowController.FindMatch);
        startWithBotsButton.onClick.AddListener(flowController.RequestStartWithBots);
        readyToggleButton.onClick.AddListener(flowController.ToggleReady);
        cancelButton.onClick.AddListener(flowController.Cancel);
    }

    void OnEnable()
    {
        flowController.PhaseChanged += OnPhaseChanged;
        OnPhaseChanged(flowController.Phase);
    }

    void OnDisable()
    {
        flowController.PhaseChanged -= OnPhaseChanged;
    }

    void Update()
    {
        var phase = flowController.Phase;

        if (phase == MatchmakingPhase.Connecting)
        {
            loadingText.text = $"Loading Game... {Mathf.FloorToInt(Time.realtimeSinceStartup - awakeRealtime)}s";
            return;
        }

        var session = flowController.Session;
        if (session == null) return;

        if (phase == MatchmakingPhase.Searching || phase == MatchmakingPhase.WaitingSolo || phase == MatchmakingPhase.WaitingReady)
            searchingText.text = $"Finding player... {Mathf.FloorToInt(session.ElapsedSeconds)}s";

        if (phase == MatchmakingPhase.WaitingReady)
            readyToggleLabel.text = session.IsReady ? "Unready" : "Don't wait, play with computer players";
    }

    void OnPhaseChanged(MatchmakingPhase phase)
    {
        bool connecting = phase == MatchmakingPhase.Connecting;
        bool waiting = !connecting && phase != MatchmakingPhase.Idle && phase != MatchmakingPhase.Starting;

        idlePanel.SetActive(connecting || phase == MatchmakingPhase.Idle);
        findMatchButton.interactable = phase == MatchmakingPhase.Idle;
        loadingText.gameObject.SetActive(connecting);

        searchingPanel.SetActive(waiting);
        soloBotPanel.SetActive(phase == MatchmakingPhase.WaitingSolo);
        readyPanel.SetActive(phase == MatchmakingPhase.WaitingReady);
        startingPanel.SetActive(phase == MatchmakingPhase.Starting);
        cancelButton.gameObject.SetActive(waiting);
    }
}
