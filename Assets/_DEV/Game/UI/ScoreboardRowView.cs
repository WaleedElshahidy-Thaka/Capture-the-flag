using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row of the in-game scoreboard (FD-01): seat colour, rank, name, score, delta to the leader,
// and a crown marker on the holder. Every field is optional, so it fits whatever the row prefab has.
// Only touches the UI when a value actually changes - InGameUI refreshes it every frame.
public class ScoreboardRowView : MonoBehaviour
{
    [SerializeField] Image frame;
    [SerializeField] TMP_Text rankText;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text scoreText;
    [Tooltip("{0} = the score. Mockup: \"{0} pts\".")]
    [SerializeField] string scoreFormat = "{0} pts";
    [Tooltip("FD-01: \"the most important number on the scoreboard\" - e.g. -140 on every non-leader row.")]
    [SerializeField] TMP_Text deltaText;
    [Tooltip("Shown on the Crown holder's row.")]
    [SerializeField] GameObject crownIcon;
    [Tooltip("Shown on your own row (FD-01: your row is persistently highlighted).")]
    [SerializeField] GameObject localHighlight;

    [Header("Colours")]
    [SerializeField] Color localNameColor = Color.white;
    [SerializeField] Color otherNameColor = new Color(1f, 1f, 1f, 0.85f);

    int lastRank = -1, lastScore = int.MinValue, lastDelta = int.MinValue;
    string lastName;
    Color lastSeat;
    bool lastLocal, lastCrown, initialised;

    public void Show(int rank, string playerName, int score, int deltaToLeader, Color seatColor, bool isLocal, bool hasCrown)
    {
        if (!initialised || seatColor != lastSeat)
        {
            if (frame != null) frame.color = seatColor;
            lastSeat = seatColor;
        }

        if (rankText != null && rank != lastRank) rankText.text = rank.ToString();

        if (nameText != null && (playerName != lastName || isLocal != lastLocal || !initialised))
        {
            nameText.text = string.IsNullOrEmpty(playerName) ? "PLAYER" : playerName.ToUpperInvariant();
            nameText.color = isLocal ? localNameColor : otherNameColor;
            nameText.fontStyle = isLocal ? FontStyles.Bold : FontStyles.Normal;
        }

        if (scoreText != null && score != lastScore)
            scoreText.text = (scoreFormat ?? "{0}").Replace("{0}", score.ToString());

        if (deltaText != null && deltaToLeader != lastDelta)
            deltaText.text = deltaToLeader > 0 ? $"-{deltaToLeader}" : ""; // leader shows nothing

        if (crownIcon != null && (hasCrown != lastCrown || !initialised)) crownIcon.SetActive(hasCrown);
        if (localHighlight != null && (isLocal != lastLocal || !initialised)) localHighlight.SetActive(isLocal);

        lastRank = rank; lastName = playerName; lastScore = score; lastDelta = deltaToLeader;
        lastLocal = isLocal; lastCrown = hasCrown; initialised = true;
    }
}
