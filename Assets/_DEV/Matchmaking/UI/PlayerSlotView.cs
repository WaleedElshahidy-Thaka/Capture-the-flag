using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row of the lobby's player list (the PlayerSlot prefab): coloured frame, level, name, status.
// Three looks, set by MatchmakingScreen:
//   ShowPlayer  - frame in the seat's colour, name, a tick when ready or a spinner while not
//   ShowEmpty   - grey frame, "FINDING PLAYER ...", no level, no icon
public class PlayerSlotView : MonoBehaviour
{
    [SerializeField] Image frame;
    [SerializeField] TMP_Text levelText;
    [SerializeField] TMP_Text playerNameText;
    [SerializeField] Image statusIcon;

    [Header("Status icons")]
    [SerializeField] Sprite readySprite;    // the tick
    [SerializeField] Sprite waitingSprite;  // the spinner ring
    [SerializeField] float spinnerDegreesPerSecond = 270f;

    [Header("Empty slot")]
    [SerializeField] string emptyText = "FINDING PLAYER ...";
    [SerializeField] Color emptyFrameColor = new Color(0.35f, 0.38f, 0.48f, 1f);
    [SerializeField] Color emptyTextColor = new Color(1f, 1f, 1f, 0.85f);

    bool spinning;

    public void ShowPlayer(string playerName, int level, Color seatColor, bool ready, bool isLocal)
    {
        if (frame != null) frame.color = seatColor;

        if (levelText != null)
        {
            // No level system yet: hide the level rather than show a fake one.
            bool hasLevel = level >= 0;
            levelText.gameObject.SetActive(hasLevel);
            if (hasLevel) levelText.text = $"LV. {level}";
        }

        if (playerNameText != null)
        {
            playerNameText.text = string.IsNullOrEmpty(playerName) ? "PLAYER" : playerName.ToUpperInvariant();
            playerNameText.color = Color.white;
            playerNameText.fontStyle = isLocal ? FontStyles.Bold | FontStyles.UpperCase : FontStyles.UpperCase;
        }

        if (statusIcon != null)
        {
            statusIcon.gameObject.SetActive(true);
            statusIcon.sprite = ready ? readySprite : waitingSprite;
            statusIcon.color = seatColor;
            if (ready) statusIcon.rectTransform.localRotation = Quaternion.identity;
        }

        spinning = !ready;
    }

    public void ShowEmpty()
    {
        if (frame != null) frame.color = emptyFrameColor;
        if (levelText != null) levelText.gameObject.SetActive(false);

        if (playerNameText != null)
        {
            playerNameText.text = emptyText;
            playerNameText.color = emptyTextColor;
            playerNameText.fontStyle = FontStyles.UpperCase;
        }

        if (statusIcon != null) statusIcon.gameObject.SetActive(false);
        spinning = false;
    }

    void Update()
    {
        if (spinning && statusIcon != null)
            statusIcon.rectTransform.Rotate(0f, 0f, -spinnerDegreesPerSecond * Time.unscaledDeltaTime);
    }
}
