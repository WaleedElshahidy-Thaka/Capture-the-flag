using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row of the results board (mockup: # / LV / NAME / BOT-YOU tag / POSSESSION / SCORE PTS).
// Every field is optional, so it fits whatever the row prefab has.
public class ResultRowView : MonoBehaviour
{
    [Tooltip("Row background, tinted with the seat colour. Draw it white/grey so the tint shows true.")]
    [SerializeField] Image frame;
    [SerializeField, Range(0f, 1f)] float frameAlpha = 0.85f;
    [SerializeField] TMP_Text rankText;
    [Tooltip("No level system yet - hidden.")]
    [SerializeField] TMP_Text levelText;
    [SerializeField] TMP_Text nameText;

    [Header("Tag (BOT / YOU)")]
    [SerializeField] GameObject tagBadge;
    [SerializeField] TMP_Text tagText;
    [SerializeField] string botTag = "BOT";
    [SerializeField] string youTag = "YOU";

    [Header("Numbers")]
    [SerializeField] TMP_Text possessionText;
    [Tooltip("Just the number - put \"PTS\" in its own small text in the prefab, as in the mockup.")]
    [SerializeField] TMP_Text scoreText;

    [Header("Highlight")]
    [Tooltip("The glowing outline. Shown on your row: gold when you won, cyan when you didn't.")]
    [SerializeField] Graphic highlight;

    public void Show(int rank, MatchResultEntry entry, Color seatColor, bool highlighted, Color highlightColor)
    {
        if (frame != null)
        {
            seatColor.a = frameAlpha;
            frame.color = seatColor;
        }

        if (rankText != null) rankText.text = rank.ToString();
        if (levelText != null) levelText.gameObject.SetActive(false);
        if (nameText != null) nameText.text = (entry.name ?? "PLAYER").ToUpperInvariant();

        string label = entry.isLocal ? youTag : entry.isBot ? botTag : null;
        if (tagBadge != null) tagBadge.SetActive(label != null);
        if (tagText != null && label != null) tagText.text = label;

        if (possessionText != null) possessionText.text = ResultFormat.Time(entry.possessionSeconds);
        if (scoreText != null) scoreText.text = entry.score.ToString();

        // Switch the outline's Graphic on/off, never its GameObject: if the highlight is on the row's
        // own object (or a parent of the row's content), SetActive(false) would hide the whole row.
        if (highlight != null)
        {
            highlight.gameObject.SetActive(highlighted);
            if (highlighted) highlight.color = highlightColor;
        }
    }

    // Shows or hides a child part of the row. Refuses to hide the row itself (or anything above it),
    // which would make the whole row disappear - a wrongly assigned field can't blank the board.
    public void SetVisible(GameObject part, bool visible)
    {
        if (part == null) return;
        if (part == gameObject || transform.IsChildOf(part.transform))
        {
            Debug.LogWarning($"[ResultRowView] '{part.name}' is the row itself - assign a child object instead.", this);
            return;
        }
        if (part.activeSelf != visible) part.SetActive(visible);
    }
}

// Shared number formats for the results screen.
public static class ResultFormat
{
    // 0:34
    public static string Time(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    // 1.9 KM, or 850 M under a kilometre.
    public static string Distance(float metres) =>
        metres >= 1000f ? $"{metres / 1000f:0.0} KM" : $"{Mathf.RoundToInt(metres)} M";
}
