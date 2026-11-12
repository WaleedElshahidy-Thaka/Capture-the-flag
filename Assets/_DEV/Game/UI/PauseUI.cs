using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// The in-match pause menu (PauseUI) from the mockups: PAUSED with Resume / How to play / Options /
// Exit to main menu / Exit to desktop, and the OPTIONS page with music and sound-effects volume.
//
// It's an online match, so nothing actually pauses: "THE MATCH IS STILL RUNNING FOR EVERYONE".
// While the menu is open this player's car gets no input (PlayerInputSampler) and coasts.
//
//   Escape / gamepad Start  - open; inside the menu it means Back (Options -> Paused -> closed)
//   Back arrow (top left)   - the same Back
//   Exit to main menu       - leaves the running match and returns to the lobby (idle)
//
// Only opens while a match is running (countdown included), and closes itself when the match ends
// or the player leaves it, so it never sits on top of the results or the lobby.
[RequireComponent(typeof(CanvasGroup))]
public class PauseUI : MonoBehaviour
{
    // Read by PlayerInputSampler: true while this player's menu is open.
    public static bool IsOpen { get; private set; }

    [SerializeField] MatchmakingFlowController flowController;

    [Header("Pages")]
    [Tooltip("Controllers: the Resume / How to play / Options / Exit buttons.")]
    [SerializeField] GameObject mainPage;
    [Tooltip("Settings: the Options page.")]
    [SerializeField] GameObject optionsPage;
    [Tooltip("Optional How to play page. Without one, the How to play button is hidden.")]
    [SerializeField] GameObject howToPlayPage;
    [Tooltip("Always shown while open (BG, MenuTitle, BackToGameBTN).")]
    [SerializeField] GameObject[] alwaysShown;
    [SerializeField, Min(0f)] float fadeSeconds = 0.2f;

    [Header("Title")]
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text subtitleText;
    [SerializeField] string pausedTitle = "PAUSED";
    [SerializeField] string optionsTitle = "OPTIONS";
    [SerializeField] string howToPlayTitle = "HOW TO PLAY";
    [SerializeField] string subtitle = "THE MATCH IS STILL RUNNING FOR EVERYONE";

    [Header("Buttons")]
    [SerializeField] Button resumeButton;
    [SerializeField] Button howToPlayButton;
    [SerializeField] Button optionsButton;
    [SerializeField] Button exitToMainMenuButton;
    [Tooltip("Optional - the mockup has it, the hierarchy may not yet.")]
    [SerializeField] Button exitToDesktopButton;
    [Tooltip("Settings/BackBTN.")]
    [SerializeField] Button optionsBackButton;
    [Tooltip("BackToGameBTN, the arrow in the top left: Back from any page.")]
    [SerializeField] Button backArrowButton;

    [Header("Audio")]
    [Tooltip("Optional AudioMixer with exposed MusicVolume / SFXVolume. See GameAudioSettings.")]
    [SerializeField] AudioMixer audioMixer;

    enum Page { Closed, Main, Options, HowToPlay }
    Page page = Page.Closed;
    CanvasGroup group;
    Coroutine fade;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (flowController == null) flowController = FindFirstObjectByType<MatchmakingFlowController>();
        GameAudioSettings.Init(audioMixer);

        Bind(resumeButton, Close);
        Bind(howToPlayButton, () => Show(Page.HowToPlay));
        Bind(optionsButton, () => Show(Page.Options));
        Bind(exitToMainMenuButton, ExitToMainMenu);
        Bind(optionsBackButton, Back);
        Bind(backArrowButton, Back);

        if (howToPlayButton != null && howToPlayPage == null) howToPlayButton.gameObject.SetActive(false);

        Show(Page.Closed, animate: false);
    }

    void OnDisable()
    {
        IsOpen = false; // never leave the car without input if this object goes away
    }

    void Update()
    {
        // Close by itself when there's no running match to pause (results, lobby, leaving).
        if (page != Page.Closed && !MatchRunning())
        {
            Show(Page.Closed);
            return;
        }

        if (!BackPressed()) return;

        if (page == Page.Closed) { if (MatchRunning()) Show(Page.Main); }
        else Back();
    }

    // ─── Actions ─────────────────────────────────────────────────────────

    void Back()
    {
        if (page == Page.Options || page == Page.HowToPlay) Show(Page.Main);
        else Close();
    }

    void Close()
    {
        GameAudioSettings.Save();
        Show(Page.Closed);
    }

    void ExitToMainMenu()
    {
        GameAudioSettings.Save();
        Show(Page.Closed);
        flowController.LeaveMatch();
    }

    void ExitToDesktop()
    {
        GameAudioSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─── Pages ───────────────────────────────────────────────────────────

    void Show(Page next, bool animate = true)
    {
        bool open = next != Page.Closed;
        bool wasOpen = page != Page.Closed;
        page = next;
        IsOpen = open;

        group.blocksRaycasts = open;
        group.interactable = open;
        if (open != wasOpen || !animate) Fade(open ? 1f : 0f, animate);

        if (alwaysShown != null)
            foreach (var go in alwaysShown) SetActive(go, open, animate);
        SetActive(mainPage, next == Page.Main, animate);
        SetActive(optionsPage, next == Page.Options, animate);
        SetActive(howToPlayPage, next == Page.HowToPlay, animate);

        if (titleText != null)
            titleText.text = next == Page.Options ? optionsTitle : next == Page.HowToPlay ? howToPlayTitle : pausedTitle;
        if (subtitleText != null) subtitleText.text = subtitle;

        // Gamepad / keyboard navigation starts on the page's first button.
        Button first = next == Page.Main ? resumeButton : next == Page.Options ? optionsBackButton : backArrowButton;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(open && first != null ? first.gameObject : null);
    }

    void Fade(float to, bool animate)
    {
        if (fade != null) StopCoroutine(fade);
        if (!animate || fadeSeconds <= 0f || !isActiveAndEnabled) group.alpha = to;
        else fade = StartCoroutine(FadeRoutine(to));
    }

    System.Collections.IEnumerator FadeRoutine(float to)
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

    // ─── Helpers ─────────────────────────────────────────────────────────

    // A match is on: countdown or playing, and not finished.
    static bool MatchRunning()
    {
        var timer = MatchTimerState.Instance;
        return timer != null && timer.Object != null && timer.Object.IsValid &&
               (timer.InCountdown || timer.IsPlaying) && !timer.Finished;
    }

    static bool BackPressed()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
               (gamepad != null && gamepad.startButton.wasPressedThisFrame);
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
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
