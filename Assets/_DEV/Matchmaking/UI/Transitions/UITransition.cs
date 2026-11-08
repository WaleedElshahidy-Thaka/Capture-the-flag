using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Animates a UI object in when it's switched on and out when it's hidden: fade, slide in from an
// edge, or pop (scale up with a little overshoot). Add it to any panel, button or row - nothing else
// is needed for the "in" animation, because it plays whenever the object becomes active.
//
//   Show()  - switch on and animate in (or reverse a hide that's still playing)
//   Hide()  - animate out, then switch off
//   Punch() - a quick scale bounce, for "something changed here" (a player joined, a button press)
//
// Inside a Horizontal/Vertical Layout Group the layout owns the position, so slide styles fall back
// to Pop there automatically. Runs on unscaled time, so it works even if the game is paused.
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
[DisallowMultipleComponent]
public class UITransition : MonoBehaviour
{
    public enum Style { Fade, FromLeft, FromRight, FromTop, FromBottom, Pop }

    [SerializeField] Style style = Style.Pop;
    [SerializeField, Min(0.01f)] float duration = 0.35f;
    [Tooltip("Wait before animating in. Use small steps (0.05-0.1 s) across panels so they arrive one after another.")]
    [SerializeField, Min(0f)] float delay = 0f;
    [Tooltip("How far a slide starts from its resting place, in canvas pixels.")]
    [SerializeField] float slideDistance = 160f;
    [Tooltip("Pop starts at this scale and overshoots slightly past 1 before settling.")]
    [SerializeField, Range(0f, 1f)] float popStartScale = 0.6f;
    [Tooltip("Out animations are quicker than in, so hiding never feels slow.")]
    [SerializeField, Range(0.2f, 1f)] float outSpeedFactor = 0.6f;

    [Header("Punch")]
    [SerializeField] float punchScale = 0.12f;
    [SerializeField, Min(0.01f)] float punchDuration = 0.22f;

    RectTransform rect;
    CanvasGroup group;
    Vector2 restPosition;
    Vector3 restScale = Vector3.one;
    bool captured;

    Coroutine running;
    Coroutine punching;
    bool hiding;

    public bool IsHiding => hiding;

    void Awake() => Capture();

    void OnEnable() => PlayIn();

    void OnDisable()
    {
        // Switched off (by us or anyone else): stop and park at rest, so the next show starts clean.
        StopAll();
        ApplyRest();
        hiding = false;
    }

    // ─── Public API ──────────────────────────────────────────────────────

    public void Show()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true); // OnEnable plays the animation
        else if (hiding) PlayIn();                               // change of mind mid-hide
    }

    public void Hide()
    {
        if (!gameObject.activeSelf || hiding) return;

        // A parent is already off: nothing would be seen, and coroutines can't run anyway.
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        hiding = true;
        group.interactable = false;
        group.blocksRaycasts = false;
        StopAll();
        running = StartCoroutine(Animate(false, 0f));
    }

    // Instant, no animation - for setting the starting state.
    public void SetVisibleImmediate(bool visible)
    {
        StopAll();
        hiding = false;
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (visible) ApplyRest();
    }

    public void Punch()
    {
        if (!isActiveAndEnabled || running != null) return; // don't fight a show/hide
        if (punching != null) StopCoroutine(punching);
        punching = StartCoroutine(PunchRoutine());
    }

    // ─── Animation ───────────────────────────────────────────────────────

    void PlayIn()
    {
        Capture();
        StopAll();
        hiding = false;
        if (!isActiveAndEnabled) return;
        running = StartCoroutine(Animate(true, delay + StaggerDelay()));
    }

    IEnumerator Animate(bool show, float wait)
    {
        Style effective = EffectiveStyle();

        if (show)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
            Apply(effective, 0f); // start hidden, so nothing flashes during the delay
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
        }

        float length = show ? duration : duration * outSpeedFactor;
        float time = 0f;
        while (time < length)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / length);
            float progress = show
                ? (effective == Style.Pop ? EaseOutBack(k) : EaseOutCubic(k))
                : 1f - EaseInCubic(k);
            Apply(effective, progress);
            yield return null;
        }

        running = null;

        if (show)
        {
            ApplyRest();
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        else
        {
            hiding = false;
            gameObject.SetActive(false); // OnDisable resets to rest for next time
        }
    }

    IEnumerator PunchRoutine()
    {
        float time = 0f;
        while (time < punchDuration)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / punchDuration);
            rect.localScale = restScale * (1f + punchScale * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        rect.localScale = restScale;
        punching = null;
    }

    // progress: 0 = fully hidden, 1 = at rest (Pop may overshoot past 1).
    void Apply(Style s, float progress)
    {
        group.alpha = Mathf.Clamp01(progress);

        if (s == Style.Pop)
        {
            rect.localScale = restScale * Mathf.LerpUnclamped(popStartScale, 1f, progress);
            return;
        }

        rect.localScale = restScale;
        if (s != Style.Fade)
            rect.anchoredPosition = restPosition + SlideOffset(s) * (1f - progress);
    }

    void ApplyRest()
    {
        if (!captured) return;
        group.alpha = 1f;
        rect.localScale = restScale;
        if (!InLayoutGroup()) rect.anchoredPosition = restPosition;
    }

    Vector2 SlideOffset(Style s)
    {
        switch (s)
        {
            case Style.FromLeft: return new Vector2(-slideDistance, 0f);
            case Style.FromRight: return new Vector2(slideDistance, 0f);
            case Style.FromTop: return new Vector2(0f, slideDistance);
            case Style.FromBottom: return new Vector2(0f, -slideDistance);
            default: return Vector2.zero;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    void Capture()
    {
        if (captured) return;
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        restPosition = rect.anchoredPosition;
        restScale = rect.localScale;
        captured = true;
    }

    Style EffectiveStyle()
    {
        bool slides = style != Style.Fade && style != Style.Pop;
        return slides && InLayoutGroup() ? Style.Pop : style;
    }

    bool InLayoutGroup()
    {
        Transform parent = transform.parent;
        if (parent == null || parent.GetComponent<LayoutGroup>() == null) return false;
        return !(TryGetComponent(out LayoutElement element) && element.ignoreLayout);
    }

    float StaggerDelay()
    {
        Transform parent = transform.parent;
        return parent != null && parent.TryGetComponent(out UIStaggerChildren stagger)
            ? stagger.DelayFor(transform)
            : 0f;
    }

    void StopAll()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        if (punching != null) { StopCoroutine(punching); punching = null; }
    }

    static float EaseOutCubic(float k) => 1f - Mathf.Pow(1f - k, 3f);
    static float EaseInCubic(float k) => k * k * k;

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }
}
