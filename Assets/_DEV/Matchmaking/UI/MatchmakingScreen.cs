using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pure UI binding, no matchmaking logic of its own - mirrors the shelved WaitingRoomScreen's
// role. One screen, panels toggled per MatchmakingPhase, matching the reference doc's
// "one screen, two states" framing (here: Idle / Searching / WaitingSolo / WaitingReady /
// Starting).
public class MatchmakingScreen : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [SerializeField] GameObject idlePanel;
    [SerializeField] Button findMatchButton;

    [SerializeField] GameObject searchingPanel;
    [SerializeField] TMP_Text searchingText; // "Finding player... {elapsed}s"

    [SerializeField] GameObject soloBotPanel;
    [SerializeField] Button startWithBotsButton;

    [SerializeField] GameObject readyPanel;
    [SerializeField] Button readyToggleButton;
    [SerializeField] TMP_Text readyToggleLabel;

    [SerializeField] GameObject startingPanel;
    [SerializeField] Button cancelButton;

    void Awake()
    {
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
        var session = flowController.Session;
        if (session == null) return;

        if (phase == MatchmakingPhase.Searching || phase == MatchmakingPhase.WaitingSolo || phase == MatchmakingPhase.WaitingReady)
            searchingText.text = $"Finding player... {Mathf.FloorToInt(session.ElapsedSeconds)}s";

        if (phase == MatchmakingPhase.WaitingReady)
            readyToggleLabel.text = session.IsReady ? "Unready" : "Don't wait, play with computer players";
    }

    void OnPhaseChanged(MatchmakingPhase phase)
    {
        idlePanel.SetActive(phase == MatchmakingPhase.Idle);
        searchingPanel.SetActive(phase != MatchmakingPhase.Idle && phase != MatchmakingPhase.Starting);
        soloBotPanel.SetActive(phase == MatchmakingPhase.WaitingSolo);
        readyPanel.SetActive(phase == MatchmakingPhase.WaitingReady);
        startingPanel.SetActive(phase == MatchmakingPhase.Starting);
        cancelButton.gameObject.SetActive(phase != MatchmakingPhase.Idle && phase != MatchmakingPhase.Starting);
    }
}
