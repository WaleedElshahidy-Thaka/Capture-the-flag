using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderAudioController : MonoBehaviour
{
    [SerializeField] private Sprite[] imagesChangeable;
    [SerializeField] private Image icon;
    [SerializeField] private Image backGround;
    [SerializeField] private Image SliderFill;
    [SerializeField] private TMP_Text percentageText;

    private Slider audioSlider;
    void Start()
    {
        audioSlider = GetComponent<Slider>();
        audioSlider.onValueChanged.AddListener((value) => UpdateAudioSliderIcon(value));
        //ApplyVolume();
    }

    private void UpdateAudioSliderIcon(float value)
    {
        UpdatePercentage(value); //update the percentage text based on the slider value

        if (value == 0)
        {
            icon.sprite = imagesChangeable[2];
            backGround.sprite = imagesChangeable[0];// make the background image Red when the slider is at 0
            SliderFill.enabled = false; // disable the fill image when the slider is at 0
        }
        else if(value > 0f && value <= 0.25f )
        {
            icon.sprite = imagesChangeable[3];
        }
        else if(value > 0.25f && value < 0.75f)
        {
            icon.sprite = imagesChangeable[4];
        }
        else if(value > 0.75f && value <= 1f)
        {
            icon.sprite = imagesChangeable[5];
        }
        // make the background image Normal when the slider is bigger than 0 
        if(value > 0)
        {
            backGround.sprite = imagesChangeable[1];
            SliderFill.enabled = true; // disable the fill image when the slider is at 0

        }
    }

    private void UpdatePercentage(float value)
    {
        percentageText.text = Mathf.RoundToInt(value * 100).ToString() + "%";
    }
    //public void ApplyVolume()
    //{
    //    //float volume = PlayerDataStore.Instance.GetSettings().AudioVolume;
    //    audioSlider.value = volume;
    //    UpdateAudioSliderIcon(volume);
    //}

}
