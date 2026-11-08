using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The end-of-match screen (ResultUI): headline, final scoreboard sorted by score, and Play Again.
// The third half of the UI, next to LobbyUI and InGameUI; it fades only its own object.
//
//   ResultHeader           - "YOU WIN!" / "DRAW!" / "MATCH OVER" and who won
//   FinalScoreBoardPanel   - every car, highest score first, in its seat colour; equal scores share a rank
//   ResultActionPanel      - Play Again: leaves the finished match and starts a new search, which
//                            hands the screen back to LobbyUI
//
// Shows the MatchResult snapshot taken at the final whistle, not the live roster, so players
// leaving afterwards can't change the board. Must be active from scene start (it listens for the
// end of the match); it stays invisible until then.
[RequireComponent(typeof(CanvasGroup))]
public class ResultsUI : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flowController;

    [Header("Panels")]
    [SerializeField] GameObject background;
    [SerializeField] GameObject resultHeader;
    [SerializeField] GameObject finalScoreBoardPanel;
    [SerializeField] GameObject resultActionPanel;
    [SerializeField, Min(0f)] float fadeSeconds = 0.4f;

    [Header("Header")]
    [SerializeField] TMP_Text resultTitle;
    [SerializeField] TMP_Text winnerText;
    [SerializeField] string winTitle = "YOU WIN!";
    [SerializeField] string drawTitle = "DRAW!";
    [SerializeField] string otherTitle = "MATCH OVER";
    [Tooltip("{0} = the winner's name.")]
    [SerializeField] string winnerFormat = "WINNER: {0}";
    [SerializeField] string drawWinnerText = "TOP SCORE SHARED";

    [Header("Scoreboard")]
    [SerializeField] TMP_Text scoreboardTitle;
    [SerializeField] string scoreboardTitleText = "FINAL SCORE";
    [SerializeField] Transform playerSlotContainer;
    [Tooltip("The PlayerSlot(FinalScoreBoard) prefab. Used to add rows beyond those already in the container.")]
    [SerializeField] ScoreboardRowView rowPrefab;

    [Header("Actions")]
    [SerializeField] Button playAgainButton;

    CanvasGroup group;
    Coroutine fade;
    bool visible;
    readonly List<ScoreboardRowView> rows = new List<ScoreboardRowView>();
    readonly List<MatchResultEntry> sorted = new List<MatchResultEntry>();

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (flowController == null) flowController = FindFirstObjectByType<MatchmakingFlowController>();

        foreach (Transform child in playerSlotContainer)
            if (child.TryGetComponent(out ScoreboardRowView row)) rows.Add(row);

        playAgainButton.onClick.AddListener(OnPlayAgain);
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

    // Back in the lobby (Play Again, or anything else that returns there): results go away.
    void OnPhaseChanged(MatchmakingPhase phase)
    {
        if (visible && phase.IsPreMatch()) SetVisible(false, animate: true);
    }

    void OnPlayAgain()
    {
        playAgainButton.interactable = false; // one press is enough
        flowController.PlayAgain();
    }

    void SetVisible(bool show, bool animate)
    {
        visible = show;
        group.blocksRaycasts = show;
        group.interactable = show;
        if (show) playAgainButton.interactable = true;

        if (fade != null) StopCoroutine(fade);
        if (!animate || fadeSeconds <= 0f || !isActiveAndEnabled) group.alpha = show ? 1f : 0f;
        else fade = StartCoroutine(Fade(show ? 1f : 0f));

        // Panels with a UITransition arrive one after another; the rows stagger in with UIStaggerChildren.
        SetActive(background, show, animate);
        SetActive(resultHeader, show, animate);
        SetActive(finalScoreBoardPanel, show, animate);
        SetActive(resultActionPanel, show, animate);
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

        // Header
        if (resultTitle != null)
            resultTitle.text = result.localWon ? winTitle : result.isDraw ? drawTitle : otherTitle;
        if (winnerText != null)
            winnerText.text = result.isDraw ? drawWinnerText
                                            : (winnerFormat ?? "{0}").Replace("{0}", result.winnerName.ToUpperInvariant());

        if (scoreboardTitle != null) scoreboardTitle.text = scoreboardTitleText;

        // Sorted by score, highest first (equal scores in seat order, same on every machine).
        sorted.Clear();
        sorted.AddRange(result.entries);
        sorted.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : a.slot.CompareTo(b.slot));

        while (rows.Count < sorted.Count && rowPrefab != null)
            rows.Add(Instantiate(rowPrefab, playerSlotContainer));

        int leader = sorted.Count > 0 ? sorted[0].score : 0;
        int rank = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < sorted.Count;
            rows[i].gameObject.SetActive(used);
            if (!used) continue;

            var entry = sorted[i];
            // Standard competition ranking: equal scores share a rank (1, 1, 3).
            if (i == 0 || entry.score != sorted[i - 1].score) rank = i + 1;

            rows[i].Show(rank, entry.name, entry.score, leader - entry.score,
                         SeatPalette.Get(entry.slot), entry.isLocal, hasCrown: false);
        }

        if (rows.Count < sorted.Count)
            Debug.LogWarning($"[ResultsUI] {sorted.Count} results but only {rows.Count} rows - assign the row prefab.", this);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

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