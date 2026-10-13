using TMPro;
using UnityEngine;
using UnityEngine.UI;

// View for MatchmakingFlowController: shows one side panel per phase and forwards button
// clicks. No matchmaking logic of its own.
public class MatchmakingScreen : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [SerializeField] GameObject idlePanel;
    [SerializeField] Button quickMatchButton;

    [SerializeField] GameObject searchingPanel;
    [SerializeField] TMP_Text searchingText;
    [SerializeField] TMP_Text playersFoundText;

    [SerializeField] GameObject foundPanel;
    [SerializeField] TMP_Text foundText;

    [SerializeField] GameObject soloBotPanel;
    [SerializeField] Button startWithBotsButton;

    [SerializeField] GameObject readyPanel;
    [SerializeField] Button readyToggleButton;
    [SerializeField] TMP_Text readyToggleLabel;

    [SerializeField] Button cancelButton;

    [SerializeField] GameObject reconnectingPanel;
    [SerializeField] GameObject resumingPanel;
    [SerializeField] TMP_Text resumingText;

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

    void OnPhaseChanged(MatchmakingPhase phase)
    {
        idlePanel.SetActive(phase == MatchmakingPhase.Idle);
        searchingPanel.SetActive(phase.ShowsSearchTimer());
        foundPanel.SetActive(phase == MatchmakingPhase.Found);
        soloBotPanel.SetActive(phase == MatchmakingPhase.WaitingSolo);
        readyPanel.SetActive(phase == MatchmakingPhase.WaitingReady);
        cancelButton.gameObject.SetActive(phase.IsCancellable());
        reconnectingPanel.SetActive(phase == MatchmakingPhase.Reconnecting);
        resumingPanel.SetActive(phase == MatchmakingPhase.Resuming);
    }

    // Live text for whichever panel is showing.
    void Update()
    {
        var phase = flowController.Phase;
        var session = flowController.Session;

        if (phase == MatchmakingPhase.Resuming) RefreshResuming(session);
        else if (phase == MatchmakingPhase.Found) RefreshFound(session);
        else if (phase.ShowsSearchTimer()) RefreshSearching(phase, session);
    }

    void RefreshResuming(IActiveQuickMatchSession session)
    {
        float remaining = session != null ? session.ResumeSecondsRemaining : -1f;
        resumingText.text = remaining < 0f
            ? "Waiting for players..."
            : $"Game resumes in {Mathf.CeilToInt(remaining)}";
    }

    void RefreshFound(IActiveQuickMatchSession session)
    {
        if (session != null)
            foundText.text = $"Player found! Joining lobby in {Mathf.CeilToInt(session.LobbyJoinSecondsRemaining)}";
    }

    void RefreshSearching(MatchmakingPhase phase, IActiveQuickMatchSession session)
    {
        // The flow controller's timer runs continuously from the click; the session's starts later.
        searchingText.text = $"Searching for players... {Mathf.FloorToInt(flowController.SearchElapsedSeconds)}s";

        int found = session != null ? session.LobbyCount : 0;
        bool showFound = session != null && session.InLobby && found >= 2;
        if (playersFoundText.gameObject.activeSelf != showFound) playersFoundText.gameObject.SetActive(showFound);
        if (showFound) playersFoundText.text = $"{found} / {session.MaxPlayers} players found";

        if (phase == MatchmakingPhase.WaitingReady && session != null)
            readyToggleLabel.text = session.IsReady ? "Unready" : "Play with computer players";
    }
}
