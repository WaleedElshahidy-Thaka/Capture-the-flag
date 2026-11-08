using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The in-game half of the UI (InGameUI). The lobby half is LobbyUI. Read-only: it shows replicated
// match state and changes nothing.
//
//   ScoreBoardPanel   - live scores, sorted, with delta to leader and the Crown holder marked (FD-01)
//   TimerPanel        - "STARTS IN 3" during the countdown, then M:SS; red + pulse in the last 10 s
//   PowerUPPanel      - your one slot: empty, reveal roll on pickup, held item, effect running (FD-04)
//   StreakScorePanel  - YOUR current streak score: points earned since you took the Crown
//
// Visible from the start countdown until the final whistle; the results screen takes over after.
// Panels with a UITransition animate in and out.
[RequireComponent(typeof(CanvasGroup))]
public class InGameUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject scoreBoardPanel;
    [SerializeField] GameObject timerPanel;
    [SerializeField] GameObject powerUpPanel;
    [SerializeField] GameObject streakScorePanel;
    [SerializeField, Min(0f)] float fadeSeconds = 0.3f;

    [Header("Scoreboard")]
    [SerializeField] TMP_Text scoreboardTitle;
    [SerializeField] Transform playerSlotContainer;
    [Tooltip("Optional. Used to add rows if the container has fewer than six.")]
    [SerializeField] ScoreboardRowView rowPrefab;

    [Header("Timer")]
    [SerializeField] TMP_Text timerTitle;
    [SerializeField] TMP_Text timerText;
    [Tooltip("Pulses once a second in the final seconds. Usually TimerPanel/Content.")]
    [SerializeField] Transform timerContent;
    [SerializeField] Color timerNormalColor = Color.white;
    [SerializeField] Color timerFinalColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("FD-01: colour shift and pulse for the final 10 s.")]
    [SerializeField, Min(0)] int finalSeconds = 10;

    [Header("Power-up slot")]
    [SerializeField] Image powerUpIcon;
    [Tooltip("One per item, in PowerUpType order: Overdrive, Grapple Field, Damper Field, Oil Slick, Repulse Pulse.")]
    [SerializeField] Sprite[] itemSprites = new Sprite[5];
    [Tooltip("The empty slot stays visible so players learn it exists (FD-04).")]
    [SerializeField] Sprite emptySprite;
    [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] Color heldColor = Color.white;
    [SerializeField] Color activeColor = new Color(0.4f, 1f, 1f);
    [Tooltip("Optional key prompt next to the icon.")]
    [SerializeField] TMP_Text keyPromptText;
    [SerializeField] string keyPrompt = "F";
    [Tooltip("FD-04 reveal: the slot rolls through items for about half a second, then settles.")]
    [SerializeField, Min(0f)] float revealSeconds = 0.5f;
    [SerializeField, Min(0.02f)] float revealStepSeconds = 0.06f;

    [Header("Streak (yours only)")]
    [SerializeField] TMP_Text streakScoreText;
    [Tooltip("{0} = points earned in your current streak.")]
    [SerializeField] string streakFormat = "{0}";
    [Tooltip("Off: always shown (0 while you don't hold the Crown). On: only shown while you hold it.")]
    [SerializeField] bool hideWhenNotHolding = false;


    CanvasGroup group;
    bool visible;
    bool appliedOnce;

    readonly List<ScoreboardRowView> rows = new List<ScoreboardRowView>();
    readonly List<PlayerMatchState> sorted = new List<PlayerMatchState>();
    readonly Dictionary<NetworkId, int> lastRank = new Dictionary<NetworkId, int>();

    int lastTimerSecond = -1;
    float timerPulse;
    Vector3 timerBaseScale = Vector3.one;

    PowerUpType lastHeld = PowerUpType.None;
    float revealEndsAt;
    float iconPulse;
    Vector3 iconBaseScale = Vector3.one;

    bool wasHolding;
    int streakStartScore;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (timerContent != null) timerBaseScale = timerContent.localScale;
        if (powerUpIcon != null) iconBaseScale = powerUpIcon.rectTransform.localScale;
        if (keyPromptText != null) keyPromptText.text = keyPrompt;
        CollectRows();
        ApplyVisible(false, animate: false);
    }

    void Update()
    {
        var timer = MatchTimerState.Instance;
        bool show = timer != null && timer.HasStateSafe() && timer.IsStartedOrPlaying() && !timer.Finished;

        if (show != visible || !appliedOnce)
            ApplyVisible(show, animate: appliedOnce);
        appliedOnce = true;

        if (!show) return;

        RefreshTimer(timer);
        RefreshScoreboard();
        RefreshPowerUp();
        RefreshStreak();
        AnimatePulses();
    }

    // ─── Show / hide ─────────────────────────────────────────────────────

    void ApplyVisible(bool show, bool animate)
    {
        visible = show;
        group.blocksRaycasts = false; // HUD only - never steals clicks
        group.interactable = false;

        if (!animate || fadeSeconds <= 0f) group.alpha = show ? 1f : 0f;
        else StartFade(show ? 1f : 0f);

        SetActive(scoreBoardPanel, show, animate);
        SetActive(timerPanel, show, animate);
        SetActive(powerUpPanel, show, animate);
        SetActive(streakScorePanel, show && !hideWhenNotHolding, animate);

        if (!show)
        {
            // Fresh state for the next match.
            lastRank.Clear();
            lastTimerSecond = -1;
            lastHeld = PowerUpType.None;
            wasHolding = false;
        }
    }

    Coroutine fade;
    void StartFade(float to)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(to));
    }

    System.Collections.IEnumerator FadeRoutine(float to)
    {
        float from = group.alpha;
        float time = 0f;
        while (time < fadeSeconds)
        {
            time += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(time / fadeSeconds));
            yield return null;
        }
        group.alpha = to;
        fade = null;
    }

    // ─── Timer ───────────────────────────────────────────────────────────

    void RefreshTimer(MatchTimerState timer)
    {
        if (timer.InCountdown)
        {
            SetText(timerTitle, "STARTS IN");
            SetText(timerText, Mathf.CeilToInt(timer.CountdownSecondsRemaining).ToString());
            SetColor(timerText, timerNormalColor);
            return;
        }

        int seconds = Mathf.CeilToInt(timer.SecondsRemaining);
        bool final = seconds <= finalSeconds;

        SetText(timerTitle, "TIME");
        SetText(timerText, $"{seconds / 60}:{seconds % 60:00}");
        SetColor(timerText, final ? timerFinalColor : timerNormalColor);

        // One pulse per second in the final stretch.
        if (final && seconds != lastTimerSecond && seconds > 0) timerPulse = 1f;
        lastTimerSecond = seconds;
    }

    // ─── Scoreboard ──────────────────────────────────────────────────────

    void RefreshScoreboard()
    {
        SetText(scoreboardTitle, "SCORE");

        sorted.Clear();
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
            if (cars[i] != null && cars[i].HasState) sorted.Add(cars[i]);

        // Highest score first; ties keep seat order so rows don't flicker.
        sorted.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : a.Slot.CompareTo(b.Slot));

        int leaderScore = sorted.Count > 0 ? sorted[0].Score : 0;
        var crown = CrownMatchState.Instance;
        var local = PlayerRoster.Local;

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < sorted.Count;
            if (rows[i].gameObject.activeSelf != used) rows[i].gameObject.SetActive(used);
            if (!used) continue;

            var car = sorted[i];
            bool hasCrown = crown != null && crown.IsHolder(car.Object);

            rows[i].Show(i + 1, car.DisplayName.ToString(), car.Score, leaderScore - car.Score,
                         SeatColor(car.Slot), car == local, hasCrown);

            // Moved up: bounce the row. FD-01 - "the animation is the feedback".
            NetworkId id = car.Object.Id;
            if (lastRank.TryGetValue(id, out int previous) && i < previous) Punch(rows[i].gameObject);
            lastRank[id] = i;
        }
    }

    // ─── Power-up slot ───────────────────────────────────────────────────

    void RefreshPowerUp()
    {
        if (powerUpIcon == null) return;

        var local = PlayerRoster.Local;
        PowerUpType held = PowerUpType.None;
        if (local != null && local.TryGetComponent(out PowerUpInventory inventory)) held = inventory.HeldItem;

        // Just picked something up: roll, then settle (FD-04 reveal).
        if (held != PowerUpType.None && lastHeld == PowerUpType.None)
            revealEndsAt = Time.unscaledTime + revealSeconds;
        lastHeld = held;

        if (held != PowerUpType.None)
        {
            float left = revealEndsAt - Time.unscaledTime;
            if (left > 0f)
            {
                int step = Mathf.FloorToInt((revealSeconds - left) / revealStepSeconds);
                SetIcon(SpriteAt(step), heldColor);
                return;
            }

            if (powerUpIcon.sprite != ItemSprite(held)) iconPulse = 1f; // the settle
            SetIcon(ItemSprite(held), heldColor);
            SetActive(keyPromptText != null ? keyPromptText.gameObject : null, true, false);
            return;
        }

        SetActive(keyPromptText != null ? keyPromptText.gameObject : null, false, false);

        // Slot empty but an effect is running: show it, gently blinking.
        PowerUpType active = ActiveEffect(local);
        if (active != PowerUpType.None)
        {
            Color c = activeColor;
            c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            SetIcon(ItemSprite(active), c);
            return;
        }

        SetIcon(emptySprite, emptyColor);
    }

    static PowerUpType ActiveEffect(PlayerMatchState local)
    {
        if (local == null) return PowerUpType.None;
        if (local.TryGetComponent(out DamperField damper) && damper.IsActive) return PowerUpType.DamperField;
        if (local.TryGetComponent(out GrappleField grapple) && grapple.IsActive) return PowerUpType.GrappleField;
        if (local.TryGetComponent(out RepulsePulse repulse) && repulse.IsCharging) return PowerUpType.RepulsePulse;
        return PowerUpType.None;
    }

    Sprite ItemSprite(PowerUpType item)
    {
        int index = (int)item - 1; // None = 0
        return itemSprites != null && index >= 0 && index < itemSprites.Length ? itemSprites[index] : emptySprite;
    }

    Sprite SpriteAt(int step) =>
        itemSprites != null && itemSprites.Length > 0 ? itemSprites[step % itemSprites.Length] : emptySprite;

    void SetIcon(Sprite sprite, Color color)
    {
        if (powerUpIcon.sprite != sprite) powerUpIcon.sprite = sprite;
        if (powerUpIcon.color != color) powerUpIcon.color = color;
        powerUpIcon.enabled = sprite != null;
    }

    // ─── Your streak ─────────────────────────────────────────────────────

    // Each player sees only their own streak: the points earned since they took the Crown.
    // Losing the Crown ends the streak and it drops back to 0. Uses the replicated score, so it's exact.
    void RefreshStreak()
    {
        var local = PlayerRoster.Local;
        var crown = CrownMatchState.Instance;
        bool holding = local != null && crown != null && crown.IsHolder(local.Object);

        if (holding && !wasHolding)
        {
            streakStartScore = local.Score; // a new streak starts from the score you had at pickup
            Punch(streakScorePanel);
        }
        wasHolding = holding;

        if (hideWhenNotHolding) SetActive(streakScorePanel, holding, true);

        int streak = holding ? Mathf.Max(0, local.Score - streakStartScore) : 0;
        // Plain replace, not string.Format: a typo in the inspector text (or an old value like
        // "+{0} {1}s") must not throw every frame and freeze the HUD.
        SetText(streakScoreText, (streakFormat ?? "{0}").Replace("{0}", streak.ToString()));
    }

    // ─── Pulses ──────────────────────────────────────────────────────────

    void AnimatePulses()
    {
        float decay = Time.unscaledDeltaTime * 5f;

        if (timerContent != null)
        {
            timerPulse = Mathf.Max(0f, timerPulse - decay);
            timerContent.localScale = timerBaseScale * (1f + 0.15f * timerPulse);
        }

        if (powerUpIcon != null)
        {
            iconPulse = Mathf.Max(0f, iconPulse - decay);
            powerUpIcon.rectTransform.localScale = iconBaseScale * (1f + 0.25f * iconPulse);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    void CollectRows()
    {
        rows.Clear();
        if (playerSlotContainer == null) return;

        foreach (Transform child in playerSlotContainer)
            if (child.TryGetComponent(out ScoreboardRowView row)) rows.Add(row);

        while (rows.Count < MatchmakingConfig.MaxPlayers && rowPrefab != null)
            rows.Add(Instantiate(rowPrefab, playerSlotContainer));

        if (rows.Count < MatchmakingConfig.MaxPlayers)
            Debug.LogWarning($"[InGameUI] {rows.Count} scoreboard rows for {MatchmakingConfig.MaxPlayers} cars - " +
                             "add ScoreboardRowView to each PlayerSlot or assign the row prefab.", this);
    }

    // Same colour as the car's ring and the other half of the UI (SeatPalette).
    static Color SeatColor(int slot) => SeatPalette.Get(slot);

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

    static void Punch(GameObject go)
    {
        if (go != null && go.TryGetComponent(out UITransition transition)) transition.Punch();
    }

    static void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    static void SetColor(TMP_Text label, Color color)
    {
        if (label != null && label.color != color) label.color = color;
    }
}

// Small readability helpers over MatchTimerState's public surface.
static class MatchTimerStateUIExtensions
{
    // The timer object can exist before its networked state is readable.
    public static bool HasStateSafe(this MatchTimerState timer) =>
        timer.Object != null && timer.Object.IsValid;

    // Countdown or match running.
    public static bool IsStartedOrPlaying(this MatchTimerState timer) =>
        timer.InCountdown || timer.IsPlaying;
}