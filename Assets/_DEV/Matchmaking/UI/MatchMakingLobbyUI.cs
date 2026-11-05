using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchMakingLobbyUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private MatchmakingFlowController flowController;

    [Header("Idle")]
    [SerializeField] private GameObject idlePanel;
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button startWithBotsButton;

    [Header("Player List")]
    [SerializeField] private GameObject playerListPanel;

    [Header("Waiting For Players")]
    [SerializeField] private GameObject waitingForPlayersPanel;
    [SerializeField] private TMP_Text waitingTitleText;
    [SerializeField] private TMP_Text playersFoundText;

    [Header("Countdown")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownTitleText;
    [SerializeField] private TMP_Text countdownText;

    [Header("Lobby Actions")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button leaveLobbyButton;

    [Header("Connection States")]
    [SerializeField] private GameObject reconnectingPanel;
    [SerializeField] private GameObject resumingPanel;
    [SerializeField] private TMP_Text resumingText;

    private int lastSearchSeconds = -1;
    private int lastCountdownSeconds = -1;
    private int lastResumeSeconds = -1;
    private int lastPlayersFound = -1;
    private bool lastReadyState;

    private void OnEnable()
    {
        // Subscribe to gameplay events.
        flowController.PhaseChanged += OnPhaseChanged;

        // Subscribe to UI button clicks.
        quickMatchButton.onClick.AddListener(flowController.FindMatch);
        startWithBotsButton.onClick.AddListener(flowController.RequestStartWithBots);
        readyButton.onClick.AddListener(flowController.ToggleReady);
        leaveLobbyButton.onClick.AddListener(flowController.Cancel);

        // Display the current state immediately.
        OnPhaseChanged(flowController.Phase);
        RefreshUI();
    }

    private void OnDisable()
    {
        flowController.PhaseChanged -= OnPhaseChanged;

        quickMatchButton.onClick.RemoveListener(flowController.FindMatch);
        startWithBotsButton.onClick.RemoveListener(flowController.RequestStartWithBots);
        readyButton.onClick.RemoveListener(flowController.ToggleReady);
        leaveLobbyButton.onClick.RemoveListener(flowController.Cancel);
    }

    private void OnPhaseChanged(MatchmakingPhase phase)
    {
        bool isIdle = phase == MatchmakingPhase.Idle;
        bool isSearching = phase.ShowsSearchTimer();
        bool isFound = phase == MatchmakingPhase.Found;
        bool isWaitingSolo = phase == MatchmakingPhase.WaitingSolo;
        bool isWaitingReady = phase == MatchmakingPhase.WaitingReady;
        bool isReconnecting = phase == MatchmakingPhase.Reconnecting;
        bool isResuming = phase == MatchmakingPhase.Resuming;

        idlePanel.SetActive(isIdle);

        playerListPanel.SetActive(
            isSearching || isFound || isWaitingSolo || isWaitingReady
        );

        waitingForPlayersPanel.SetActive(
            isSearching || isWaitingSolo || isWaitingReady
        );

        countdownPanel.SetActive(isFound);

        readyButton.gameObject.SetActive(isWaitingReady);

        leaveLobbyButton.gameObject.SetActive(phase.IsCancellable());

        reconnectingPanel.SetActive(isReconnecting);
        resumingPanel.SetActive(isResuming);

        // Reset cached values when changing phase.
        ResetDisplayCache();

        RefreshUI();
    }

    private void Update()
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        MatchmakingPhase phase = flowController.Phase;
        IActiveQuickMatchSession session = flowController.Session;

        if (phase.ShowsSearchTimer())
        {
            RefreshSearching(session);
        }

        if (phase == MatchmakingPhase.Found)
        {
            RefreshCountdown(session);
        }

        if (phase == MatchmakingPhase.WaitingReady)
        {
            RefreshReady(session);
        }

        if (phase == MatchmakingPhase.Resuming)
        {
            RefreshResuming(session);
        }
    }

    private void RefreshSearching(IActiveQuickMatchSession session)
    {
        int elapsedSeconds =
            Mathf.FloorToInt(flowController.SearchElapsedSeconds);

        if (lastSearchSeconds != elapsedSeconds)
        {
            waitingTitleText.text =
                $"SEARCHING FOR PLAYERS... {elapsedSeconds}s";

            lastSearchSeconds = elapsedSeconds;
        }

        int found = session != null ? session.LobbyCount : 0;
        int maxPlayers = session != null ? session.MaxPlayers : 0;

        bool showFound =
            session != null && session.InLobby && found >= 2;

        playersFoundText.gameObject.SetActive(showFound);

        if (showFound && lastPlayersFound != found)
        {
            playersFoundText.text = $"{found} / {maxPlayers} PLAYERS FOUND";
            lastPlayersFound = found;
        }
    }

    private void RefreshCountdown(IActiveQuickMatchSession session)
    {
        if (session == null)
            return;

        int remainingSeconds =
            Mathf.CeilToInt(session.LobbyJoinSecondsRemaining);

        if (lastCountdownSeconds == remainingSeconds)
            return;

        countdownTitleText.text = "GAME WILL START IN";
        countdownText.text = $"{remainingSeconds} SEC";

        lastCountdownSeconds = remainingSeconds;
    }

    private void RefreshReady(IActiveQuickMatchSession session)
    {
        if (session == null)
            return;

        if (lastReadyState == session.IsReady)
            return;

        readyButtonText.text = session.IsReady
            ? "CANCEL READY"
            : "READY";

        lastReadyState = session.IsReady;
    }

    private void RefreshResuming(IActiveQuickMatchSession session)
    {
        float remaining =
            session != null ? session.ResumeSecondsRemaining : -1f;

        if (remaining < 0f)
        {
            resumingText.text = "WAITING FOR PLAYERS...";
            return;
        }

        int seconds = Mathf.CeilToInt(remaining);

        if (lastResumeSeconds == seconds)
            return;

        resumingText.text = $"GAME RESUMES IN {seconds}";
        lastResumeSeconds = seconds;
    }

    private void ResetDisplayCache()
    {
        lastSearchSeconds = -1;
        lastCountdownSeconds = -1;
        lastResumeSeconds = -1;
        lastPlayersFound = -1;
        lastReadyState = !flowController.Session?.IsReady ?? false;
    }
}
