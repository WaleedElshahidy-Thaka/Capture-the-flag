using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One marker of PlayerIndicatorsUI: sits over a car while it's on screen, and slides to the screen
// edge with an arrow pointing at it when it's off screen or behind you. Every field except the
// RectTransform is optional, so it fits whatever art the marker prefab has.
[RequireComponent(typeof(RectTransform))]
public class PlayerIndicatorView : MonoBehaviour
{
    [Tooltip("Tinted with the car's seat colour. Draw it white so the tint gives the exact colour.")]
    [SerializeField] Image icon;
    [Tooltip("Points at the car. Shown only at the screen edge. Draw it pointing RIGHT (0 degrees).")]
    [SerializeField] RectTransform arrow;
    [SerializeField] Image arrowImage;
    [Tooltip("Shown on the Crown holder's marker.")]
    [SerializeField] GameObject crownBadge;
    [Tooltip("Optional distance in metres.")]
    [SerializeField] TMP_Text distanceText;

    [Header("Crown holder")]
    [Tooltip("The holder's marker is bigger, so it stands out from everyone else.")]
    [SerializeField] float crownHolderScale = 1.35f;

    RectTransform rect;
    Color lastColor = new Color(-1f, 0f, 0f);
    bool lastCrown, initialised;
    int lastMetres = -1;

    public RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;

    public void Show(Vector2 localPosition, bool atEdge, float arrowAngle, Color seatColor,
                     bool isCrownHolder, float distance, bool showDistance)
    {
        Rect.anchoredPosition = localPosition;

        if (arrow != null)
        {
            if (arrow.gameObject.activeSelf != atEdge) arrow.gameObject.SetActive(atEdge);
            if (atEdge) arrow.localRotation = Quaternion.Euler(0f, 0f, arrowAngle);
        }

        if (seatColor != lastColor)
        {
            if (icon != null) icon.color = seatColor;
            if (arrowImage != null) arrowImage.color = seatColor;
            lastColor = seatColor;
        }

        if (!initialised || isCrownHolder != lastCrown)
        {
            if (crownBadge != null) crownBadge.SetActive(isCrownHolder);
            Rect.localScale = Vector3.one * (isCrownHolder ? crownHolderScale : 1f);
            lastCrown = isCrownHolder;
        }

        if (distanceText != null)
        {
            if (distanceText.gameObject.activeSelf != showDistance) distanceText.gameObject.SetActive(showDistance);
            int metres = Mathf.RoundToInt(distance);
            if (showDistance && metres != lastMetres)
            {
                distanceText.text = $"{metres} m";
                lastMetres = metres;
            }
        }

        initialised = true;
    }
}
