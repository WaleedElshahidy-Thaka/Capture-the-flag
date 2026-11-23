using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Waiting" loading animation: the icons under this transform hop one after another in a wave,
/// with a small squash-and-stretch when they land.
/// Put it on the container (e.g. PlayerCountContainer). It works with a Horizontal/Vertical Layout Group:
/// it only adds an offset on top of the position the layout gives each icon, and it notices when the
/// layout moves an icon. Icons spawned or removed at runtime are picked up automatically.
/// </summary>
[DisallowMultipleComponent]
public sealed class LoadingWaveAnimator : MonoBehaviour
{
    [Header("Hop")]
    [SerializeField, Min(0f)] float hopHeight = 14f;
    [SerializeField, Min(0.05f)] float hopDuration = 0.35f;
    [SerializeField, Min(0f)] float delayBetweenIcons = 0.12f;
    [SerializeField, Min(0f)] float pauseAfterWave = 0.4f;

    [Header("Squash & Stretch")]
    [SerializeField, Range(0f, 0.5f)] float stretch = 0.12f;
    [SerializeField, Range(0f, 0.5f)] float landSquash = 0.15f;
    [SerializeField, Min(0.01f)] float landDuration = 0.12f;

    [Header("Options")]
    [Tooltip("Animate the icon's first child instead of the icon itself, if it has one. " +
             "Keeps the icon's own RectTransform untouched for the layout group.")]
    [SerializeField] bool animateFirstChild = false;
    [Tooltip("Keeps playing when Time.timeScale is 0 (e.g. paused lobby).")]
    [SerializeField] bool useUnscaledTime = true;
    [SerializeField] bool playOnEnable = true;

    sealed class Track
    {
        public RectTransform rect;
        public Vector2 basePos;
        public Vector2 lastWritten;
        public Vector3 baseScale;
    }

    readonly List<Track> _tracks = new List<Track>();
    int _lastChildCount = -1;
    float _time;
    bool _playing;

    public bool IsPlaying => _playing;

    public void Play()
    {
        _time = 0f;
        _playing = true;
    }

    public void Stop()
    {
        _playing = false;
        RestoreAll();
    }

    void OnEnable()
    {
        Rebuild();
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        _playing = false;
        RestoreAll();
    }

    void OnTransformChildrenChanged() => _lastChildCount = -1; // icons were spawned/removed

    // LateUpdate so we run after the layout group has positioned the icons this frame.
    void LateUpdate()
    {
        if (!_playing) return;
        if (_lastChildCount != transform.childCount) Rebuild();

        _time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        int count = _tracks.Count;
        if (count == 0) return;

        float iconTime = hopDuration + landDuration;
        float cycle = (count - 1) * delayBetweenIcons + iconTime + pauseAfterWave;
        float t = _time % cycle;

        for (int i = 0; i < count; i++)
        {
            Track tr = _tracks[i];
            if (tr.rect == null) { _lastChildCount = -1; continue; }

            // If the layout moved the icon since our last write, its new position is the base.
            if (tr.rect.anchoredPosition != tr.lastWritten) tr.basePos = tr.rect.anchoredPosition;

            float local = t - i * delayBetweenIcons;
            float y = 0f, sx = 1f, sy = 1f;

            if (local >= 0f && local < hopDuration)
            {
                float p = local / hopDuration;            // 0..1 through the hop
                y = 4f * p * (1f - p) * hopHeight;        // parabola: up then down
                float s = Mathf.Sin(p * Mathf.PI) * stretch;
                sy = 1f + s; sx = 1f - s * 0.5f;          // stretch while in the air
            }
            else if (local >= hopDuration && local < iconTime)
            {
                float p = (local - hopDuration) / landDuration;
                float s = Mathf.Sin(p * Mathf.PI) * landSquash;
                sy = 1f - s; sx = 1f + s * 0.5f;          // squash on landing
            }

            Vector2 pos = tr.basePos + new Vector2(0f, y);
            tr.rect.anchoredPosition = pos;
            tr.lastWritten = pos;
            tr.rect.localScale = new Vector3(tr.baseScale.x * sx, tr.baseScale.y * sy, tr.baseScale.z);
        }
    }

    void Rebuild()
    {
        RestoreAll();
        _tracks.Clear();

        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeInHierarchy) continue;

            Transform target = animateFirstChild && child.childCount > 0 ? child.GetChild(0) : child;
            if (!(target is RectTransform rect)) continue;

            _tracks.Add(new Track
            {
                rect = rect,
                basePos = rect.anchoredPosition,
                lastWritten = rect.anchoredPosition,
                baseScale = rect.localScale
            });
        }
        _lastChildCount = transform.childCount;
    }

    void RestoreAll()
    {
        foreach (Track tr in _tracks)
        {
            if (tr.rect == null) continue;
            if (tr.rect.anchoredPosition == tr.lastWritten) tr.rect.anchoredPosition = tr.basePos;
            tr.rect.localScale = tr.baseScale;
            tr.lastWritten = tr.basePos;
        }
    }
}
