using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The DRIFT bar from the in-game mockup: your drift reserve filling toward stage 3, a marker at each
// stage threshold, and a flash while a stage window is open ("press Shift now").
// Reads your car's replicated drift state - display only.
//
// Note: the HLD listed "speed or charge meters" under Not shipping. This mockup adds one, so design
// has changed its mind - worth updating the HLD.
public class DriftMeterUI : MonoBehaviour
{
    [Tooltip("The bar fill. Image Type = Filled, Fill Method = Horizontal.")]
    [SerializeField] Image fill;
    [Tooltip("The bar's full-width area; the stage markers are placed along it.")]
    [SerializeField] RectTransform barArea;
    [Tooltip("Thin markers for stage 1, 2 and 3, children of the bar area.")]
    [SerializeField] RectTransform[] stageMarkers = new RectTransform[3];
    [SerializeField] TMP_Text label;

    [Header("Colours")]
    [SerializeField] Color chargingColor = new Color(0.25f, 0.9f, 1f);
    [Tooltip("Bar colour once each stage is reached (1, 2, 3).")]
    [SerializeField] Color[] stageColors =
    {
        new Color(0.3f, 0.6f, 1f),
        new Color(1f, 0.85f, 0.2f),
        new Color(1f, 0.35f, 0.9f),
    };
    [SerializeField] Color markerColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] Color markerReachedColor = Color.white;
    [Tooltip("The fill flashes this colour while a stage window is open.")]
    [SerializeField] Color windowFlashColor = Color.white;
    [SerializeField, Min(0.1f)] float flashSpeed = 12f;

    float lastStage1, lastStage2, lastStage3;

    void Update()
    {
        var local = PlayerRoster.Local;
        if (local == null || !local.HasState || !local.TryGetComponent(out PlayerMovement move)) return;

        HandlingValues h = HandlingValuesProvider.Current;
        float max = Mathf.Max(1f, h.Stage3Threshold);
        float reserve = Mathf.Clamp(move.DriftReserveValue, 0f, max);
        int stage = DriftBoost.StageFor(reserve, h);

        PlaceMarkers(h, max);

        if (fill != null)
        {
            fill.fillAmount = reserve / max;

            Color c = stage > 0 && stageColors != null && stageColors.Length >= stage ? stageColors[stage - 1] : chargingColor;
            if (move.DriftWindowStage > 0)
                c = Color.Lerp(c, windowFlashColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * flashSpeed));
            fill.color = c;
        }

        if (stageMarkers != null)
            for (int i = 0; i < stageMarkers.Length; i++)
                if (stageMarkers[i] != null && stageMarkers[i].TryGetComponent(out Graphic g))
                    g.color = move.DriftStageReached > i ? markerReachedColor : markerColor;

        if (label != null)
        {
            string text = move.DriftWindowStage > 0 ? $"BOOST {move.DriftWindowStage}!" : "DRIFT";
            if (label.text != text) label.text = text;
        }
    }

    // Markers sit at each threshold's share of the bar. Only moves them when the tuning changes.
    void PlaceMarkers(HandlingValues h, float max)
    {
        if (stageMarkers == null || barArea == null) return;
        if (h.Stage1Threshold == lastStage1 && h.Stage2Threshold == lastStage2 && h.Stage3Threshold == lastStage3) return;
        lastStage1 = h.Stage1Threshold; lastStage2 = h.Stage2Threshold; lastStage3 = h.Stage3Threshold;

        float[] at = { h.Stage1Threshold / max, h.Stage2Threshold / max, 1f };
        for (int i = 0; i < stageMarkers.Length && i < at.Length; i++)
        {
            var m = stageMarkers[i];
            if (m == null) continue;
            m.anchorMin = new Vector2(at[i], m.anchorMin.y);
            m.anchorMax = new Vector2(at[i], m.anchorMax.y);
            m.anchoredPosition = new Vector2(0f, m.anchoredPosition.y);
        }
    }
}
