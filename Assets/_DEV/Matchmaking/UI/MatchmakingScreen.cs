using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pure UI binding, no matchmaking logic of its own. Compact side panels per MatchmakingPhase
// (Idle / Searching / Found / WaitingSolo / WaitingReady) kept off to the left so the robots
// stay in view; nothing at all once the match is on (Starting). Two full-screen overlays cover
// a host migration (Reconnecting / Resuming).
public class MatchmakingScreen : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [SerializeField] GameObject idlePanel;
    [SerializeField] Button quickMatchButton;

    [SerializeField] GameObject searchingPanel;
    [SerializeField] TMP_Text searchingText;    // "Searching for players... {elapsed}s"
    [SerializeField] TMP_Text playersFoundText; // "{n} players found" once there are 2+

    [SerializeField] GameObject foundPanel;
    [SerializeField] TMP_Text foundText;        // "Player found! Joining lobby in {n}"

    [SerializeField] GameObject soloBotPanel;
    [SerializeField] Button startWithBotsButton;

    [SerializeField] GameObject readyPanel;
    [SerializeField] Button readyToggleButton;
    [SerializeField] TMP_Text readyToggleLabel; // "Play with computer players" / "Unready"

    [SerializeField] Button cancelButton;

    [SerializeField] GameObject reconnectingPanel; // "Host disconnected - reconnecting..."
    [SerializeField] GameObject resumingPanel;
    [SerializeField] TMP_Text resumingText;        // "Waiting for players..." / "Game resumes in {n}"

    void Awake()
    {
        quickMatchButton.onClick.AddListener(flowController.FindMatch);
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
        var session = flowController.Session;

        if (phase == MatchmakingPhase.Resuming)
        {
            float remaining = session != null ? session.ResumeSecondsRemaining : -1f;
            resumingText.text = remaining < 0f
                ? "Waiting for players..."
                : $"Game resumes in {Mathf.CeilToInt(remaining)}";
            return;
        }

        if (phase == MatchmakingPhase.Found)
        {
            if (session != null)
                foundText.text = $"Player found! Joining lobby in {Mathf.CeilToInt(session.LobbyJoinSecondsRemaining)}";
            return;
        }

        if (!IsSearchingPhase(phase)) return;

        // The flow controller's timer, not the session's: it runs from the click, through the
        // connection, and on into the shared timer without a break.
        searchingText.text = $"Searching for players... {Mathf.FloorToInt(flowController.SearchElapsedSeconds)}s";

        int found = session != null ? session.LobbyCount : 0;
        bool showFound = session != null && session.InLobby && found >= 2;
        if (playersFoundText.gameObject.activeSelf != showFound) playersFoundText.gameObject.SetActive(showFound);
        if (showFound) playersFoundText.text = $"{found} / {session.MaxPlayers} players found";

        if (phase == MatchmakingPhase.WaitingReady && session != null)
            readyToggleLabel.text = session.IsReady ? "Unready" : "Play with computer players";
    }

    // The phases that show the timer panel. Found replaces it with its own message for 3 s.
    static bool IsSearchingPhase(MatchmakingPhase phase) =>
        phase == MatchmakingPhase.Searching || phase == MatchmakingPhase.WaitingSolo || phase == MatchmakingPhase.WaitingReady;

    void OnPhaseChanged(MatchmakingPhase phase)
    {
        bool searching = IsSearchingPhase(phase);

        idlePanel.SetActive(phase == MatchmakingPhase.Idle);
        searchingPanel.SetActive(searching);
        foundPanel.SetActive(phase == MatchmakingPhase.Found);
        soloBotPanel.SetActive(phase == MatchmakingPhase.WaitingSolo);
        readyPanel.SetActive(phase == MatchmakingPhase.WaitingReady);
        // Cancel stays available through the countdown - you can still back out before the lobby.
        cancelButton.gameObject.SetActive(searching || phase == MatchmakingPhase.Found);
        reconnectingPanel.SetActive(phase == MatchmakingPhase.Reconnecting);
        resumingPanel.SetActive(phase == MatchmakingPhase.Resuming);
    }
}
