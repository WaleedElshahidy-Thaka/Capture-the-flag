using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One volume row in the Options panel (MUSIC / SOUND EFFECTS): a slider, the "80%" text, and a
// speaker icon that switches to the muted icon at 0%. Saves through GameAudioSettings.
public class VolumeSettingView : MonoBehaviour
{
    [SerializeField] GameAudioSettings.Channel channel = GameAudioSettings.Channel.Music;
    [Tooltip("0..1. Min Value 0, Max Value 1, Whole Numbers off.")]
    [SerializeField] Slider slider;
    [SerializeField] TMP_Text percentText;
    [Tooltip("Shown at 0% (the crossed-out speaker).")]
    [SerializeField] GameObject mutedIcon;
    [Tooltip("Shown above 0%.")]
    [SerializeField] GameObject soundIcon;
    [Tooltip("Optional: clicking the speaker toggles mute and restores the previous volume.")]
    [SerializeField] Button muteButton;

    float volumeBeforeMute = 0.8f;

    void Awake()
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(OnSlider);
        }
        if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
    }

    // Shows the saved value each time the panel opens.
    void OnEnable()
    {
        float value = GameAudioSettings.Get(channel);
        if (slider != null) slider.SetValueWithoutNotify(value);
        ShowValue(value);
    }

    void OnSlider(float value)
    {
        GameAudioSettings.Set(channel, value);
        ShowValue(value);
    }

    void ToggleMute()
    {
        float current = GameAudioSettings.Get(channel);
        float next = current > 0f ? 0f : (volumeBeforeMute > 0f ? volumeBeforeMute : 0.8f);
        if (current > 0f) volumeBeforeMute = current;
        if (slider != null) slider.value = next; // fires OnSlider
        else OnSlider(next);
    }

    void ShowValue(float value)
    {
        int percent = Mathf.RoundToInt(value * 100f);
        if (percentText != null) percentText.text = $"{percent}%";
        if (mutedIcon != null) mutedIcon.SetActive(percent == 0);
        if (soundIcon != null) soundIcon.SetActive(percent > 0);
    }
}
