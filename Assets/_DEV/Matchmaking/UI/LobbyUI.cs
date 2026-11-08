using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The lobby half of the UI. The in-game half is InGameUI. Put this on the LobbyUI object (the
// parent of the lobby panels), NOT on a Canvas shared with the in-game UI: hiding the lobby fades
// this object's CanvasGroup, and a CanvasGroup hides everything underneath it.
// A view for MatchmakingFlowController: shows and fills the lobby panels and forwards button clicks.
// No matchmaking logic of its own - when the match starts is decided by MatchStartRule.
//
//   PlayerListPanel         - one row per seat: players who pressed READY, then "FINDING PLAYER ..."
//   WaitingForPlayersPanel  - title + six robot icons, lit for each ready player
//   CountdownPanel          - "GAME WILL START IN N SEC", 120 s down to 0
//   ReadyButton             - READY starts the countdown (finds a match), CANCEL stops it
//   LeaveLobbyButton        - also cancels, whenever backing out is allowed
//
// The match starts at 0, or at once when six players are ready.
// The lobby fades out when the match starts and back in when the player returns to the lobby.
// It never touches a Canvas component, so it can't hide the in-game UI by accident.
//
// Transitions: any panel, button, row or icon with a UITransition animates in and out instead of
// popping; without one it simply switches on and off. Rows and icons pop as players join.
[RequireComponent(typeof(CanvasGroup))]
public class LobbyUI : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [Header("Panels")]
    [SerializeField] GameObject background;
    [SerializeField] GameObject lobbyBranding;
    [SerializeField] GameObject playerListPanel;
    [SerializeField] GameObject waitingForPlayersPanel;
    [SerializeField] GameObject countdownPanel;
    [SerializeField] GameObject lobbyActionsPanel;

    [Header("Player list")]
    [SerializeField] TMP_Text playerListTitle;
    [SerializeField] Transform playerSlotsContainer;
    [Tooltip("Optional. Used to add rows if the container has fewer than six PlayerSlot instances.")]
    [SerializeField] PlayerSlotView playerSlotPrefab;

    [Header("Waiting for players")]
    [SerializeField] TMP_Text waitingTitle;
    [SerializeField] Transform playerCountContainer;
    [Tooltip("Optional. Used to add icons if the container has fewer than six PlayerCountIcon instances.")]
    [SerializeField] Image playerCountIconPrefab;
    [SerializeField] Color iconReadyColor = Color.white;
    [SerializeField] Color iconEmptyColor = new Color(1f, 1f, 1f, 0.3f);

    [Header("Countdown")]
    [SerializeField] TMP_Text countdownTitle;
    [SerializeField] TMP_Text countdownText;

    [Header("Actions")]
    [SerializeField] Button readyButton;
    [SerializeField] TMP_Text readyButtonText;
    [SerializeField] Button leaveLobbyButton;

    [Header("Text")]
    [SerializeField] string readyLabel = "READY";
    [SerializeField] string cancelLabel = "CANCEL";

    [Header("Transitions")]
    [Tooltip("How long the whole lobby takes to fade out when the match starts (and back in).")]
    [SerializeField, Min(0f)] float canvasFadeSeconds = 0.4f;

    [Header("Seat colours (seat 0 first) - row frames and status icons")]
    [SerializeField]
    Color[] seatColors =
    {
        new Color(0.30f, 0.55f, 1.00f), // blue
        new Color(1.00f, 0.90f, 0.10f), // yellow
        new Color(1.00f, 0.20f, 0.45f), // pink
        new Color(0.15f, 0.90f, 0.55f), // green
        new Color(0.65f, 0.35f, 1.00f), // purple
        new Color(1.00f, 0.55f, 0.15f), // orange
    };

    CanvasGroup canvasGroup;
    bool lobbyVisible;
    Coroutine canvasFade;
    bool appliedOnce;
    int lastReadyCount;
    readonly List<PlayerSlotView> slots = new List<PlayerSlotView>();
    readonly List<Image> countIcons = new List<Image>();
    readonly List<PlayerMatchState> readyPlayers = new List<PlayerMatchState>();

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        WarnIfSharedCanvas();
        CollectRows();
        readyButton.onClick.AddListener(OnReadyButton);
        leaveLobbyButton.onClick.AddListener(OnLeaveButton);
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

    // ─── Which panels show ───────────────────────────────────────────────

    void OnPhaseChanged(MatchmakingPhase phase)
    {
        // Idle and searching are the lobby. Everything after (the match, a host migration) belongs
        // to the in-game UI, so the lobby fades out - the component stays enabled so it
        // can switch back on when the player returns to the lobby.
        bool inLobby = phase.IsPreMatch();
        bool searching = phase.IsCancellable(); // Searching, Found, WaitingSolo, WaitingReady

        // The first time (scene start) everything is placed instantly - no animations on load.
        bool animate = appliedOnce;
        appliedOnce = true;

        FadeCanvas(inLobby, animate);

        SetActive(background, inLobby, animate);
        SetActive(lobbyBranding, inLobby, animate);
        SetActive(lobbyActionsPanel, inLobby, animate);
        SetActive(playerListPanel, searching, animate);
        SetActive(waitingForPlayersPanel, searching, animate);
        SetActive(countdownPanel, searching, animate);
        SetActive(leaveLobbyButton.gameObject, searching, animate);

        if (!searching) lastReadyCount = 0; // the rows' entrance does the work next time
        Refresh();
    }

    void Update()
    {
        if (lobbyVisible) Refresh();
    }

    // Fades the lobby in or out with this object's CanvasGroup. Hidden = alpha 0 and no clicks;
    // the panels underneath are switched off by OnPhaseChanged, so a hidden lobby costs nothing.
    void FadeCanvas(bool visible, bool animate)
    {
        if (canvasFade != null) StopCoroutine(canvasFade);
        lobbyVisible = visible;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;

        if (!animate || canvasFadeSeconds <= 0f || !isActiveAndEnabled)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            return;
        }

        canvasFade = StartCoroutine(FadeRoutine(visible));
    }

    System.Collections.IEnumerator FadeRoutine(bool visible)
    {
        float from = canvasGroup.alpha;
        float to = visible ? 1f : 0f;
        float time = 0f;
        while (time < canvasFadeSeconds)
        {
            time += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(time / canvasFadeSeconds));
            yield return null;
        }
        canvasGroup.alpha = to;
        canvasFade = null;
    }

    // The bug this prevents: LobbyUI on the root Canvas fades the root, and the in-game UI (a child
    // of the same root) fades with it.
    void WarnIfSharedCanvas()
    {
        if (GetComponentInChildren<InGameUI>(true) != null)
            Debug.LogError("[LobbyUI] The in-game UI is underneath this object, so hiding the lobby hides it too. " +
                           "Move LobbyUI onto the LobbyUI object (the parent of the lobby panels only).", this);
    }

    void Refresh()
    {
        var phase = flowController.Phase;
        if (!phase.IsPreMatch()) return;

        RefreshReadyButton(phase);

        if (!phase.IsCancellable()) return;

        GatherReadyPlayers();
        RefreshPlayerList();
        RefreshPlayerCount(phase);
        RefreshCountdown(phase, flowController.Session);
    }

    // ─── Player list and robot icons ─────────────────────────────────────

    // Pressing READY puts a player in the search, so "ready" = searching and visible in the lobby.
    // You always see yourself, even before anyone else has joined. Sorted by seat.
    void GatherReadyPlayers()
    {
        readyPlayers.Clear();
        var local = PlayerRoster.Local;
        var cars = PlayerRoster.Active;

        for (int i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            if (car == null || !car.HasState || !car.IsSearching) continue;
            if (car != local && !car.InLobby) continue;
            readyPlayers.Add(car);
        }

        readyPlayers.Sort((a, b) => a.Slot.CompareTo(b.Slot));
    }

    void RefreshPlayerList()
    {
        var local = PlayerRoster.Local;
        SetText(playerListTitle, "LOBBY");

        // Someone just joined: pop their row and their robot icon.
        if (readyPlayers.Count > lastReadyCount && lastReadyCount > 0)
        {
            for (int i = lastReadyCount; i < readyPlayers.Count; i++)
            {
                if (i < slots.Count) Punch(slots[i].gameObject);
                if (i < countIcons.Count) Punch(countIcons[i].gameObject);
            }
        }
        lastReadyCount = readyPlayers.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < readyPlayers.Count)
            {
                var player = readyPlayers[i];
                // No level system yet: -1 hides the level text.
                slots[i].ShowPlayer(player.DisplayName.ToString(), -1, SeatColor(player.Slot),
                                    ready: true, isLocal: player == local);
            }
            else
            {
                slots[i].ShowEmpty();
            }
        }
    }

    void RefreshPlayerCount(MatchmakingPhase phase)
    {
        string title =
            phase == MatchmakingPhase.Found ? "PLAYER FOUND!" :
            MatchStartRule.IsFull(readyPlayers.Count) ? "ALL PLAYERS READY!" :
            "WAITING FOR PLAYERS";
        SetText(waitingTitle, title);

        for (int i = 0; i < countIcons.Count; i++)
            countIcons[i].color = i < readyPlayers.Count ? iconReadyColor : iconEmptyColor;
    }

    // ─── Countdown ───────────────────────────────────────────────────────

    void RefreshCountdown(MatchmakingPhase phase, IActiveQuickMatchSession session)
    {
        if (phase == MatchmakingPhase.Found && session != null)
        {
            SetText(countdownTitle, "JOINING LOBBY IN");
            SetText(countdownText, $"{Mathf.CeilToInt(session.LobbyJoinSecondsRemaining)} SEC");
            return;
        }

        SetText(countdownTitle, "GAME WILL START IN");
        SetText(countdownText, $"{Mathf.CeilToInt(SecondsUntilStart(session))} SEC");
    }

    // Same rule and same inputs as the host, so the number on screen is the one that starts the match.
    float SecondsUntilStart(IActiveQuickMatchSession session)
    {
        if (session == null)
            return MatchStartRule.SecondsUntilStart(flowController.SearchElapsedSeconds, 1);

        // Alone: your own timer. Together: the lobby's shared timer and its player count.
        int players = session.InLobby ? Mathf.Max(1, session.LobbyCount) : 1;
        return MatchStartRule.SecondsUntilStart(session.ElapsedSeconds, players);
    }

    // ─── Buttons ─────────────────────────────────────────────────────────

    void RefreshReadyButton(MatchmakingPhase phase)
    {
        SetText(readyButtonText, phase == MatchmakingPhase.Idle ? readyLabel : cancelLabel);
        if (!readyButton.interactable) readyButton.interactable = true;
    }

    // READY starts the countdown; pressed again (CANCEL) it stops it.
    void OnReadyButton()
    {
        Punch(readyButton.gameObject);
        var phase = flowController.Phase;
        if (phase == MatchmakingPhase.Idle) flowController.FindMatch();
        else if (phase.IsCancellable()) flowController.Cancel();
    }

    void OnLeaveButton()
    {
        if (flowController.Phase.IsCancellable()) flowController.Cancel();
    }

    // ─── Setup and helpers ───────────────────────────────────────────────

    // Uses the PlayerSlot / PlayerCountIcon instances already in the containers and tops them up to
    // six from the prefabs, so the scene can hold anywhere from zero to six of each.
    void CollectRows()
    {
        int seats = MatchmakingConfig.MaxPlayers;

        slots.Clear();
        if (playerSlotsContainer != null)
        {
            foreach (Transform child in playerSlotsContainer)
                if (child.TryGetComponent(out PlayerSlotView view)) slots.Add(view);

            while (slots.Count < seats && playerSlotPrefab != null)
                slots.Add(Instantiate(playerSlotPrefab, playerSlotsContainer));
        }

        countIcons.Clear();
        if (playerCountContainer != null)
        {
            foreach (Transform child in playerCountContainer)
                if (child.TryGetComponent(out Image icon)) countIcons.Add(icon);

            while (countIcons.Count < seats && playerCountIconPrefab != null)
                countIcons.Add(Instantiate(playerCountIconPrefab, playerCountContainer));
        }

        if (slots.Count < seats)
            Debug.LogWarning($"[LobbyUI] {slots.Count} player rows for {seats} seats - add PlayerSlots or assign the prefab.", this);
        if (countIcons.Count < seats)
            Debug.LogWarning($"[LobbyUI] {countIcons.Count} robot icons for {seats} seats - add PlayerCountIcons or assign the prefab.", this);
    }

    Color SeatColor(int slot) =>
        seatColors != null && seatColors.Length > 0 ? seatColors[Mathf.Abs(slot) % seatColors.Length] : Color.white;

    // With a UITransition: animate in/out (or place instantly the first time). Without: plain on/off.
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

    // Only touches TMP when the text actually changes, so the per-frame refresh stays cheap.
    static void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }
}