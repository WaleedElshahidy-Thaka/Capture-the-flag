using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// The matchmaking screen (MatchMakingUI): the screen before a search - logo, match info
// (ROBOTS 6 / ROUND 90s / MODE FREE FOR ALL) and FIND MATCH. Shown while the flow is Idle: at start,
// and again after Return to Main Menu / Exit to Main Menu.
//
//   FIND MATCH  -> starts the search; this screen fades out and LobbyUI takes over (timer 0 -> 120 s)
//   Back arrow  -> whatever you hook to On Back (there's no main menu yet - e.g. quit, or a menu later)
//
// Fades only its own CanvasGroup, like the other screens.
[RequireComponent(typeof(CanvasGroup))]
public class MatchMakingUI : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [Header("Panels (shown on this screen; give them a UITransition to animate)")]
    [Tooltip("Blur-BG, LobbyBranding, MatchMackingInfo, MatchMackingActionsPanel, BackToGameBTN.")]
    [SerializeField] GameObject[] panels;
    [SerializeField, Min(0f)] float fadeSeconds = 0.3f;

    [Header("Buttons")]
    [SerializeField] Button findMatchButton;
    [SerializeField] Button backButton;
    [Tooltip("What the back arrow does. Empty = it quits the game (stops Play mode in the editor).")]
    [SerializeField] UnityEvent onBack;

    [Header("Match info")]
    [SerializeField] TMP_Text robotsText;
    [SerializeField] TMP_Text roundText;
    [SerializeField] TMP_Text modeText;
    [Tooltip("{0} = the match length in seconds, read from the CrownMatchState prefab.")]
    [SerializeField] string roundFormat = "{0}s";
    [SerializeField] string modeName = "FREE FOR ALL";
    [Tooltip("The prefab in a Resources folder that carries MatchTimerState (its Duration Seconds is the round length).")]
    [SerializeField] string matchStatePrefab = "CrownMatchState";

    CanvasGroup group;
    Coroutine fade;
    bool visible;
    bool appliedOnce;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (flowController == null) flowController = FindFirstObjectByType<MatchmakingFlowController>();

        if (findMatchButton != null) findMatchButton.onClick.AddListener(OnFindMatch);
        if (backButton != null) backButton.onClick.AddListener(OnBack);

        FillInfo();
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
        bool show = phase == MatchmakingPhase.Idle;
        bool animate = appliedOnce;
        appliedOnce = true;
        if (show == visible && animate) return;

        visible = show;
        group.blocksRaycasts = show;
        group.interactable = show;
        if (show && findMatchButton != null) findMatchButton.interactable = true;

        if (fade != null) StopCoroutine(fade);
        if (!animate || fadeSeconds <= 0f || !isActiveAndEnabled) group.alpha = show ? 1f : 0f;
        else fade = StartCoroutine(Fade(show ? 1f : 0f));

        if (panels != null)
            foreach (var panel in panels) SetActive(panel, show, animate);
    }

    void OnFindMatch()
    {
        if (flowController.Phase != MatchmakingPhase.Idle) return;
        findMatchButton.interactable = false; // one press is enough
        flowController.FindMatch();
    }

    void OnBack()
    {
        if (onBack != null && onBack.GetPersistentEventCount() > 0)
        {
            onBack.Invoke();
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ROBOTS = seats per match, ROUND = the match length on the prefab, MODE = the mode name.
    void FillInfo()
    {
        if (robotsText != null) robotsText.text = MatchmakingConfig.MaxPlayers.ToString();
        if (modeText != null) modeText.text = modeName;

        if (roundText != null)
        {
            var prefab = Resources.Load<GameObject>(matchStatePrefab);
            var timer = prefab != null ? prefab.GetComponent<MatchTimerState>() : null;
            int seconds = timer != null ? Mathf.RoundToInt(timer.ConfiguredDurationSeconds) : 90;
            roundText.text = (roundFormat ?? "{0}").Replace("{0}", seconds.ToString());
        }
    }

    System.Collections.IEnumerator Fade(float to)
    {
        float from = group.alpha, time = 0f;
        while (time < fadeSeconds)
        {
            time += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(time / fadeSeconds));
            yield return null;
        }
        group.alpha = to;
        fade = null;
    }

    static void SetActive(GameObject go, bool active, bool animate)
    {
        if (go == null) return;
        if (go.TryGetComponent(out UITransition transition))
        {
            if (!animate) transition.SetVisibleImmediate(active);
            else if (active) transition.Show();
            else transition.Hide();
            return;
        }
        if (go.activeSelf != active) go.SetActive(active);
    }
}
