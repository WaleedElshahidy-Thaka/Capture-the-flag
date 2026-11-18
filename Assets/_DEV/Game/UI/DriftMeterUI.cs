using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The DRIFT bar from the in-game mockup, built on a Slider.
//
// The bar fills from 0 for EACH stage up to the FLAG, CTR-style: steady to the middle, then slower and
// slower as it nears the flag (it lands on the flag exactly when the stage is reached) - and each stage slows harder than the last: empty -> stage 1 threshold, then back to empty -> stage 2,
// then empty -> stage 3. So it always shows "how close am I to the next boost". Reaching a stage
// opens its window ("press Shift now"): the fill and the marker flash and the label says BOOST n!
// After stage 3 the bar stays full until the reserve resets.
//
// The marker is the Slider's Handle by default - the one small mark that rides on the end of the fill.
// Reads your car's replicated drift state - display only.
//
// Note: the HLD listed "speed or charge meters" under Not shipping; the mockup adds this one.
public class DriftMeterUI : MonoBehaviour
{
    [Tooltip("The DRIFT slider. The script sets Min 0, Max 1 and makes it non-interactable.")]
    [SerializeField] Slider slider;
    [Tooltip("The fill's Image (Slider > Fill Area > Fill). Leave empty to use the slider's Fill Rect.")]
    [SerializeField] Image fill;
    [Tooltip("The one marker. Leave empty to use the slider's Handle.")]
    [SerializeField] Graphic marker;
    [SerializeField] TMP_Text label;

    [Header("Flag")]
    [Tooltip("The flag on the bar (the boost line). The fill reaches it exactly when the stage is reached, " +
             "slowing down as it gets close.")]
    [SerializeField] RectTransform flag;
    [Tooltip("Where the flag sits along the bar (0 = left end, 1 = right end).")]
    [SerializeField, Range(0.1f, 1f)] float flagPosition = 0.85f;
    [Tooltip("Move the flag to Flag Position along its parent on start. Off = leave it where you put it " +
             "(then set Flag Position to match it).")]
    [SerializeField] bool placeFlag = true;
    [Tooltip("While a boost window is open, the fill carries on from the flag to the end of the bar over the " +
             "window's length - the 'press now' zone. Off = it waits at the flag.")]
    [SerializeField] bool fillPastFlagDuringWindow = true;

    [Header("Fill feel (CTR-style)")]
    [Tooltip("Where in each stage the bar starts slowing down (0.5 = the middle).")]
    [SerializeField, Range(0.1f, 0.9f)] float slowdownStart = 0.5f;
    [Tooltip("How full the bar looks at that point, for stage 1, 2 and 3. Higher = it rushes to the middle and " +
             "then crawls - so each stage slows harder than the last. 0.5 = no slowdown (linear).")]
    [SerializeField] float[] fillAtSlowdown = { 0.6f, 0.7f, 0.8f };
    [Tooltip("Smooths the bar's movement (higher = snappier). 0 = no smoothing.")]
    [SerializeField, Min(0f)] float smoothing = 12f;

    [Header("Colours")]
    [Tooltip("Fill colour while charging toward stage 1, 2 and 3.")]
    [SerializeField]
    Color[] chargeColors =
    {
        new Color(0.25f, 0.9f, 1f),  // toward stage 1
        new Color(1f, 0.85f, 0.2f),  // toward stage 2
        new Color(1f, 0.35f, 0.9f),  // toward stage 3
    };
    [Tooltip("Fill colour once stage 3 is reached (bar full).")]
    [SerializeField] Color maxedColor = new Color(1f, 0.35f, 0.9f);
    [SerializeField] Color markerColor = Color.white;
    [Tooltip("The fill and marker flash this colour while a stage window is open.")]
    [SerializeField] Color windowFlashColor = Color.white;
    [SerializeField, Min(0.1f)] float flashSpeed = 12f;

    [Header("Text")]
    [SerializeField] string idleText = "DRIFT";
    [Tooltip("{0} = the stage whose window is open.")]
    [SerializeField] string windowText = "BOOST {0}!";

    float shownValue;
    int shownStage = -1;
    int shownWindowStage;
    float windowOpenedAt;

    // How full the bar looks for a real progress p (0..1) through a stage.
    //   before slowdownStart: a steady fill up to fillAtSlowdown
    //   after it:             an ease-out that keeps getting slower, reaching exactly 1 at the threshold
    // The ease-out starts at the same speed the steady part ends with, so there's no jolt at the middle,
    // and a higher fillAtSlowdown means a harder slowdown at the end. Each stage uses its own value.
    // Display only: the bar still fills exactly when the threshold (and its boost window) arrives.
    float Shape(float p, int stage)
    {
        float m = fillAtSlowdown != null && stage < fillAtSlowdown.Length ? fillAtSlowdown[stage] : 0.5f;
        float s = slowdownStart;
        m = Mathf.Clamp(m, s, 0.98f); // never below linear, never a dead stop

        if (p <= s) return p / s * m;

        float speedAtMiddle = m / s;                    // slope of the steady part
        float k = speedAtMiddle * (1f - s) / (1f - m);  // ease-out power that matches it
        float t = (p - s) / (1f - s);
        return m + (1f - m) * (1f - Mathf.Pow(1f - t, k));
    }

    void Awake()
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = false; // a meter, not a control
        slider.transition = Selectable.Transition.None;

        if (fill == null && slider.fillRect != null) fill = slider.fillRect.GetComponent<Image>();
        if (marker == null && slider.handleRect != null) marker = slider.handleRect.GetComponent<Graphic>();

        // Put the flag at its spot along its parent (anchors only, so it follows the bar's size).
        if (flag != null && placeFlag)
        {
            flag.anchorMin = new Vector2(flagPosition, flag.anchorMin.y);
            flag.anchorMax = new Vector2(flagPosition, flag.anchorMax.y);
            flag.anchoredPosition = new Vector2(0f, flag.anchoredPosition.y);
        }
    }

    void Update()
    {
        if (slider == null) return;

        var local = PlayerRoster.Local;
        if (local == null || !local.HasState || !local.TryGetComponent(out PlayerMovement move)) return;

        HandlingValues h = HandlingValuesProvider.Current;
        float reserve = Mathf.Max(0f, move.DriftReserveValue);
        int stage = DriftBoost.StageFor(reserve, h); // stages already reached by the reserve (0..3)

        // Progress inside the current stage: 0 at the last threshold, 1 at the next one.
        float progress;
        if (stage >= 3)
        {
            progress = 1f;
        }
        else
        {
            float from = Threshold(stage, h);
            float to = Threshold(stage + 1, h);
            progress = to > from ? Mathf.Clamp01((reserve - from) / (to - from)) : 0f;
        }
        // Shape it (slower and slower as it nears the flag), scaled so it lands exactly ON the flag when
        // the stage is reached. A new stage snaps straight back to empty, so the reset reads clearly.
        bool window = move.DriftWindowStage > 0;
        if (window && move.DriftWindowStage != shownWindowStage) windowOpenedAt = Time.unscaledTime;
        shownWindowStage = move.DriftWindowStage;

        float target;
        if (window)
        {
            // Boost window open: past the flag, toward the end of the bar over the window's length.
            float windowSeconds = Mathf.Max(0.05f, h.StageWindowTicks / (float)Mathf.Max(1, local.Runner.TickRate));
            float t = fillPastFlagDuringWindow ? Mathf.Clamp01((Time.unscaledTime - windowOpenedAt) / windowSeconds) : 0f;
            target = Mathf.Lerp(flagPosition, 1f, t);
        }
        else
        {
            target = stage >= 3 ? 1f : flagPosition * Shape(progress, stage);
        }

        if (stage != shownStage || smoothing <= 0f)
            shownValue = target;
        else
            shownValue = Mathf.Lerp(shownValue, target, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
        shownStage = stage;
        slider.SetValueWithoutNotify(shownValue);

        // Colours: the stage it's charging toward, flashing while a window is open.
        float flash = window ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * flashSpeed) : 0f;

        if (fill != null)
        {
            Color c = stage >= 3 ? maxedColor : ChargeColor(stage);
            fill.color = Color.Lerp(c, windowFlashColor, flash);
        }
        if (marker != null)
            marker.color = Color.Lerp(markerColor, windowFlashColor, flash);
        if (flag != null && flag.TryGetComponent(out Graphic flagGraphic))
            flagGraphic.color = Color.Lerp(markerColor, windowFlashColor, flash);

        if (label != null)
        {
            string text = window ? (windowText ?? "{0}").Replace("{0}", move.DriftWindowStage.ToString()) : idleText;
            if (label.text != text) label.text = text;
        }
    }

    // Threshold that starts stage n's bar: 0 for stage 0, then each stage threshold.
    static float Threshold(int stage, HandlingValues h)
    {
        switch (stage)
        {
            case 0: return 0f;
            case 1: return h.Stage1Threshold;
            case 2: return h.Stage2Threshold;
            default: return h.Stage3Threshold;
        }
    }

    Color ChargeColor(int stage) =>
        chargeColors != null && stage < chargeColors.Length ? chargeColors[stage] : Color.white;
}