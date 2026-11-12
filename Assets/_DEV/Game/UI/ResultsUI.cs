using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The end-of-match screen (ResultUI), in two looks from the mockups:
//
//   Victory  - gold accent, crown icon, "VICTORY", "YOU HELD THE CROWN THE LONGEST", your row in gold
//   So close - cyan accent, robot icon, "SO CLOSE", "54 POINTS OFF THE WIN • BETTER LUCK NEXT TIME",
//              your row in cyan
//
// Both show the final standings (rank, name, BOT/YOU, possession time, score) in seat colours, the
// four superlatives (FD-01) and RETURN TO MAIN MENU. All of it comes from the MatchResult snapshot
// taken at the final whistle, so every peer shows the same board and nobody leaving can change it.
// Order follows FD-01: score, then total possession, then longest single possession.
//
// Must be active from scene start (it listens for the end of the match); invisible until then.
[RequireComponent(typeof(CanvasGroup))]
public class ResultsUI : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [Header("Panels (shown in this order; give them a UITransition to animate)")]
    [SerializeField] GameObject[] panels;
    [SerializeField, Min(0f)] float fadeSeconds = 0.4f;

    [Header("Header")]
    [SerializeField] Image headerIcon;
    [SerializeField] Sprite victoryIcon;     // the crown
    [SerializeField] Sprite defeatIcon;      // the robot head
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text subtitleText;
    [Tooltip("Lines, glows, frames tinted with the accent colour (gold on victory, cyan otherwise).")]
    [SerializeField] Graphic[] accentGraphics;
    [Tooltip("Extra art shown only on victory (confetti, rays...).")]
    [SerializeField] GameObject[] victoryOnly;
    [Tooltip("Extra art shown only when you didn't win.")]
    [SerializeField] GameObject[] defeatOnly;

    [Header("Colours")]
    [SerializeField] Color victoryColor = new Color(1f, 0.78f, 0.15f);
    [SerializeField] Color defeatColor = new Color(0.25f, 0.9f, 1f);

    [Header("Text")]
    [SerializeField] string victoryTitle = "VICTORY";
    [SerializeField] string defeatTitle = "SO CLOSE";
    [SerializeField] string drawTitle = "DRAW";
    [SerializeField] string victorySubtitle = "YOU HELD THE CROWN THE LONGEST";
    [SerializeField] string victoryTieBreakSubtitle = "TIED ON POINTS • YOU HELD THE CROWN LONGER";
    [Tooltip("{0} = points between you and the winner.")]
    [SerializeField] string defeatSubtitle = "{0} POINTS OFF THE WIN • BETTER LUCK NEXT TIME";
    [SerializeField] string defeatTieBreakSubtitle = "TIED ON POINTS • LOST ON POSSESSION TIME";
    [SerializeField] string drawSubtitle = "TOP SCORE SHARED";

    [Header("Final standings")]
    [SerializeField] Transform rowsContainer;
    [Tooltip("Used to add rows beyond those already in the container.")]
    [SerializeField] ResultRowView rowPrefab;

    [Header("Superlatives")]
    [SerializeField] SuperlativeCardView longestHoldCard;
    [SerializeField] SuperlativeCardView mostStealsCard;
    [SerializeField] SuperlativeCardView mostSlamsCard;
    [SerializeField] SuperlativeCardView distanceCard;

    [Header("Actions")]
    [SerializeField] Button returnToMenuButton;
    [Tooltip("Optional: a second button that goes straight into a new search.")]
    [SerializeField] Button playAgainButton;

    CanvasGroup group;
    Coroutine fade;
    bool visible;
    readonly List<ResultRowView> templateRows = new List<ResultRowView>(); // rows placed in the scene
    readonly List<ResultRowView> spawnedRows = new List<ResultRowView>();  // rows made from the prefab
    readonly List<ResultRowView> rows = new List<ResultRowView>();         // this match's rows, in order

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (flowController == null) flowController = FindFirstObjectByType<MatchmakingFlowController>();

        if (rowsContainer != null)
            foreach (Transform child in rowsContainer)
                if (child.TryGetComponent(out ResultRowView row)) templateRows.Add(row);
        ClearBoard();

        if (returnToMenuButton != null) returnToMenuButton.onClick.AddListener(OnReturnToMenu);
        if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgain);
        SetVisible(false, animate: false);
    }

    void OnEnable()
    {
        MatchTimerState.MatchFinished += OnMatchFinished;
        flowController.PhaseChanged += OnPhaseChanged;
    }

    void OnDisable()
    {
        MatchTimerState.MatchFinished -= OnMatchFinished;
        flowController.PhaseChanged -= OnPhaseChanged;
    }

    // ─── Show / hide ─────────────────────────────────────────────────────

    void OnMatchFinished(MatchResult result)
    {
        Fill(result);
        SetVisible(true, animate: true);
    }

    // Back in the lobby: the results go away, and the board is wiped for the next match.
    void OnPhaseChanged(MatchmakingPhase phase)
    {
        if (visible && phase.IsPreMatch())
        {
            SetVisible(false, animate: true);
            ClearBoard();
        }
    }

    // Every match starts from an empty board: prefab rows destroyed, scene rows hidden, cards reset.
    // Nothing from the last match can show through, however many players it had.
    void ClearBoard()
    {
        foreach (var row in spawnedRows) if (row != null) Destroy(row.gameObject);
        spawnedRows.Clear();
        foreach (var row in templateRows) if (row != null) row.gameObject.SetActive(false);
        rows.Clear();

        if (longestHoldCard != null) longestHoldCard.Show(null, null, Color.white);
        if (mostStealsCard != null) mostStealsCard.Show(null, null, Color.white);
        if (mostSlamsCard != null) mostSlamsCard.Show(null, null, Color.white);
        if (distanceCard != null) distanceCard.Show(null, null, Color.white);
    }

    void OnReturnToMenu()
    {
        SetButtons(false); // one press is enough
        flowController.ReturnToMenu();
    }

    void OnPlayAgain()
    {
        SetButtons(false);
        flowController.PlayAgain();
    }

    void SetButtons(bool interactable)
    {
        if (returnToMenuButton != null) returnToMenuButton.interactable = interactable;
        if (playAgainButton != null) playAgainButton.interactable = interactable;
    }

    void SetVisible(bool show, bool animate)
    {
        visible = show;
        group.blocksRaycasts = show;
        group.interactable = show;
        if (show) SetButtons(true);

        if (fade != null) StopCoroutine(fade);
        if (!animate || fadeSeconds <= 0f || !isActiveAndEnabled) group.alpha = show ? 1f : 0f;
        else fade = StartCoroutine(Fade(show ? 1f : 0f));

        if (panels != null)
            foreach (var panel in panels) SetActive(panel, show, animate);
    }

    System.Collections.IEnumerator Fade(float to)
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

    // ─── Content ─────────────────────────────────────────────────────────

    void Fill(MatchResult result)
    {
        if (result == null) return;

        bool won = result.localWon;
        Color accent = won ? victoryColor : defeatColor;

        // Header: the look switches on whether YOU won.
        if (headerIcon != null)
        {
            Sprite icon = won ? victoryIcon : defeatIcon;
            if (icon != null) headerIcon.sprite = icon;
            headerIcon.color = accent;
        }

        if (titleText != null)
        {
            titleText.text = result.isDraw ? drawTitle : won ? victoryTitle : defeatTitle;
            titleText.color = accent;
        }

        if (subtitleText != null)
            subtitleText.text = Subtitle(result);

        if (accentGraphics != null)
            foreach (var g in accentGraphics) if (g != null) g.color = accent;

        SetAll(victoryOnly, won);
        SetAll(defeatOnly, !won);

        // Final standings, already in FD-01 order. Your row is the highlighted one.
        // Start clean every time: scene rows first, then rows made from the prefab as needed.
        ClearBoard();
        var entries = result.entries;
        foreach (var row in templateRows)
            if (row != null && rows.Count < entries.Count) rows.Add(row);
        while (rows.Count < entries.Count && rowPrefab != null && rowsContainer != null)
        {
            var row = Instantiate(rowPrefab, rowsContainer);
            spawnedRows.Add(row);
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var entry = entries[i];
            rows[i].gameObject.SetActive(true);
            rows[i].transform.SetSiblingIndex(i); // keep the on-screen order = the final order
            rows[i].Show(i + 1, entry, SeatPalette.Get(entry.slot), entry.isLocal, accent);
            rows[i].SetVisible(rowsContainer.gameObject, true);
        }

        if (rows.Count < entries.Count)
            Debug.LogWarning($"[ResultsUI] {entries.Count} results but only {rows.Count} rows - assign the row prefab.", this);

        // Superlatives
        ShowCard(longestHoldCard, result, result.longestHoldIndex, e => ResultFormat.Time(e.longestHoldSeconds));
        ShowCard(mostStealsCard, result, result.mostStealsIndex, e => e.steals.ToString());
        ShowCard(mostSlamsCard, result, result.mostSlamsIndex, e => e.slams.ToString());
        ShowCard(distanceCard, result, result.mostDistanceIndex, e => ResultFormat.Distance(e.distanceMeters));
    }

    string Subtitle(MatchResult result)
    {
        if (result.isDraw) return drawSubtitle;
        if (result.localWon) return result.wonOnTieBreak ? victoryTieBreakSubtitle : victorySubtitle;
        if (result.localPointsOffWin == 0) return defeatTieBreakSubtitle; // level on points, lost on possession
        return (defeatSubtitle ?? "{0}").Replace("{0}", result.localPointsOffWin.ToString());
    }

    static void ShowCard(SuperlativeCardView card, MatchResult result, int index,
                         System.Func<MatchResultEntry, string> value)
    {
        if (card == null) return;
        var entry = index >= 0 && index < result.entries.Count ? result.entries[index] : null;
        card.Show(entry != null ? value(entry) : null, entry, entry != null ? SeatPalette.Get(entry.slot) : Color.white);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    static void SetAll(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var go in objects) if (go != null && go.activeSelf != active) go.SetActive(active);
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
