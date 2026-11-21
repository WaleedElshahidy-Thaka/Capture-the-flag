using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The HOW TO PLAY pop-up, shared by every screen that has a GAME RULES button (MatchMakingUI,
// LobbyUI - and the pause menu can use it too). One pop-up, opened from anywhere:
//
//   Open Buttons  - every GAME RULES button; each opens the pop-up (no code on the screens)
//   Close         - the close button, a click on the dimmed background, Escape or gamepad B
//
// Modal: while it's up, the dimmed background catches all clicks, so nothing behind it can be
// pressed. It closes by itself if the game moves on underneath: opened in the lobby, it closes when
// the match starts (the 120 s timer); opened from the pause menu, it closes when the match ends.
//
// Put it on the pop-up's root, LAST among the Canvas's children so it draws on top of every screen.
[RequireComponent(typeof(CanvasGroup))]
public class HowToPlayPopup : MonoBehaviour
{
    public static HowToPlayPopup Instance { get; private set; }

    [SerializeField] MatchmakingFlowController flowController;

    [Header("Parts")]
    [Tooltip("The panel itself. Give it a UITransition (Pop) for the pop-up animation.")]
    [SerializeField] GameObject panel;
    [Tooltip("A full-screen Image behind the panel (dark, semi-transparent) with Raycast Target ON: dims the " +
             "screen and blocks clicks. Optional: a Button on it closes the pop-up when clicked.")]
    [SerializeField] GameObject dimBackground;
    [SerializeField] Button closeButton;
    [Tooltip("Every GAME RULES button that should open this pop-up.")]
    [SerializeField] Button[] openButtons;
    [SerializeField, Min(0f)] float fadeSeconds = 0.2f;

    [Header("Rules text (optional)")]
    [Tooltip("These words are replaced with the real values: {robots} {round} {unlock} {limit}")]
    [SerializeField] TMP_Text rulesText;
    [Tooltip("The prefab in a Resources folder with MatchTimerState, for {round}.")]
    [SerializeField] string matchStatePrefab = "CrownMatchState";

    public bool IsOpen { get; private set; }
    bool openedInMatch; // where it was opened: lobby screens (false) or the pause menu (true)

    CanvasGroup group;
    Coroutine fade;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();

        // Sharing an object (and so a CanvasGroup) with another screen makes them hide each other.
        if (GetComponent<PauseUI>() != null || GetComponent<LobbyUI>() != null || GetComponent<MatchMakingUI>() != null)
            Debug.LogError("[HowToPlayPopup] It's on the same object as another screen - they share one CanvasGroup " +
                           "and hide each other. Put HowToPlayPopup on its own object.", this);
        if (flowController == null) flowController = FindFirstObjectByType<MatchmakingFlowController>();

        if (openButtons != null)
            foreach (var b in openButtons) if (b != null) b.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (dimBackground != null && dimBackground.TryGetComponent(out Button dimButton))
            dimButton.onClick.AddListener(Close);

        FillRules();
        Show(false, animate: false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsOpen) return;

        // Closes by itself if the game moves on underneath it (lobby -> match, or match -> lobby).
        if (flowController != null && InMatch() != openedInMatch) { Close(); return; }

        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            Close();
    }

    public void Open()
    {
        openedInMatch = InMatch();
        Show(true, animate: true);
    }

    bool InMatch() => flowController != null && !flowController.Phase.IsPreMatch();
    public void Close() => Show(false, animate: true);

    void Show(bool open, bool animate)
    {
        IsOpen = open;
        group.blocksRaycasts = open; // modal: nothing behind can be clicked
        group.interactable = open;

        if (fade != null) StopCoroutine(fade);
        if (!animate || fadeSeconds <= 0f || !isActiveAndEnabled) group.alpha = open ? 1f : 0f;
        else fade = StartCoroutine(Fade(open ? 1f : 0f));

        SetActive(dimBackground, open, animate);
        SetActive(panel, open, animate);
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

    // {tokens} -> live values, so the rules never go out of date.
    void FillRules()
    {
        if (rulesText == null) return;

        var prefab = Resources.Load<GameObject>(matchStatePrefab);
        var timer = prefab != null ? prefab.GetComponent<MatchTimerState>() : null;
        int round = timer != null ? Mathf.RoundToInt(timer.ConfiguredDurationSeconds) : 90;

        rulesText.text = rulesText.text
            .Replace("{robots}", MatchmakingConfig.MaxPlayers.ToString())
            .Replace("{round}", round.ToString())
            .Replace("{unlock}", Mathf.RoundToInt(MatchmakingConfig.BotOptionUnlockSeconds).ToString())
            .Replace("{limit}", Mathf.RoundToInt(MatchmakingConfig.SearchDurationSeconds).ToString());
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
