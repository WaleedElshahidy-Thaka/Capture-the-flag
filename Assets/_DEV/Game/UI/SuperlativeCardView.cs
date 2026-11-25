using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One superlative card under the board (LONGEST HOLD / MOST STEALS / MOST SLAMS / DISTANCE DRIVEN):
// the award's value, and a name tag in the winner's seat colour. FD-01: so a player who came fifth
// still leaves with something they were good at. Bots are eligible.
public class SuperlativeCardView : MonoBehaviour
{
    [Tooltip("The big number. The title (\"MOST STEALS\") is fixed text in the prefab.")]
    [SerializeField] TMP_Text valueText;

    [Header("Name tag")]
    [SerializeField] GameObject nameTag;
    [Tooltip("Tinted with the winner's seat colour.")]
    [SerializeField] Image nameTagFrame;
    [Tooltip("The winner's lobby level ('LV. n'; bots / unknown show LV. 1).")]
    [SerializeField] TMP_Text levelText;
    [SerializeField] TMP_Text nameText;
    [SerializeField] string nobodyValue = "-";

    // entry == null: nobody earned it (all zero) - show a dash and no name.
    public void Show(string value, MatchResultEntry entry, Color seatColor)
    {
        if (valueText != null) valueText.text = entry != null ? value : nobodyValue;
        // Never hide the card itself through this field - only a child name tag.
        if (nameTag != null && nameTag != gameObject && !transform.IsChildOf(nameTag.transform))
            nameTag.SetActive(entry != null);
        if (entry == null) return;

        if (nameTagFrame != null) nameTagFrame.color = seatColor;
        if (levelText != null) levelText.text = PlayerLobbyProfile.LevelLabel(entry.level);
        if (nameText != null) nameText.text = (entry.name ?? "PLAYER").ToUpperInvariant();
    }
}