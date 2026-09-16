using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pure UI binding, no matchmaking logic of its own. A full-screen black Loading panel, then
// compact side panels per MatchmakingPhase (Idle / Searching / WaitingSolo / WaitingReady /
// Starting) kept off to the left so the robots stay in view.
public class MatchmakingScreen : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [SerializeField] GameObject loadingPanel;   // full-screen black until the own car is seated
    [SerializeField] TMP_Text loadingText;      // "Loading... {elapsed}s"

    [SerializeField] GameObject idlePanel;
    [SerializeField] Button quickMatchButton;

    [SerializeField] GameObject searchingPanel;
    [SerializeField] TMP_Text searchingText;    // "Searching for players... {elapsed}s"
    [SerializeField] TMP_Text playersFoundText; // "{n} players found" once there are 2+

    [SerializeField] GameObject soloBotPanel;
    [SerializeField] Button startWithBotsButton;

    [SerializeField] GameObject readyPanel;
    [SerializeField] Button readyToggleButton;
    [SerializeField] TMP_Text readyToggleLabel; // "Play with computer players" / "Unready"

    [SerializeField] GameObject startingPanel;
    [SerializeField] Button cancelButton;

    float loadStartRealtime;

    void Awake()
    {
        loadStartRealtime = Time.realtimeSinceStartup;

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

        if (phase == MatchmakingPhase.Loading)
        {
            loadingText.text = $"Loading... {Mathf.FloorToInt(Time.realtimeSinceStartup - loadStartRealtime)}s";
            return;
        }

        var session = flowController.Session;
        if (session == null) return;

        bool searching = phase == MatchmakingPhase.Searching || phase == MatchmakingPhase.WaitingSolo || phase == MatchmakingPhase.WaitingReady;
        if (!searching) return;

        searchingText.text = $"Searching for players... {Mathf.FloorToInt(session.ElapsedSeconds)}s";

        int found = session.SearchingCount;
        bool showFound = found >= 2;
        if (playersFoundText.gameObject.activeSelf != showFound) playersFoundText.gameObject.SetActive(showFound);
        if (showFound) playersFoundText.text = $"{found} / {session.MaxPlayers} players found";

        if (phase == MatchmakingPhase.WaitingReady)
            readyToggleLabel.text = session.IsReady ? "Unready" : "Play with computer players";
    }

    void OnPhaseChanged(MatchmakingPhase phase)
    {
        bool searching = phase == MatchmakingPhase.Searching || phase == MatchmakingPhase.WaitingSolo || phase == MatchmakingPhase.WaitingReady;

        loadingPanel.SetActive(phase == MatchmakingPhase.Loading);
        idlePanel.SetActive(phase == MatchmakingPhase.Idle);
        searchingPanel.SetActive(searching);
        soloBotPanel.SetActive(phase == MatchmakingPhase.WaitingSolo);
        readyPanel.SetActive(phase == MatchmakingPhase.WaitingReady);
        startingPanel.SetActive(phase == MatchmakingPhase.Starting);
        cancelButton.gameObject.SetActive(searching);
    }
}
