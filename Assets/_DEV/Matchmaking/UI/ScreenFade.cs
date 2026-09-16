using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// A full-screen black image that fades in, does one thing while the screen is black, then
// fades back out. Used for the cut between the solo view (your robot alone, before a match is
// found) and the group lobby view. Sits above every other panel on the matchmaking canvas;
// built by GameSceneSetup.
[RequireComponent(typeof(Image))]
public class ScreenFade : MonoBehaviour
{
    [SerializeField] float halfDurationSeconds = 0.3f;

    Image image;
    Coroutine running;

    public bool IsRunning => running != null;

    void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;
        SetAlpha(0f);
    }

    // atBlack runs at full black; onDone once the screen is clear again. A second call while
    // one is running is ignored - the first cut is still on screen.
    public void Run(Action atBlack, Action onDone = null)
    {
        if (running != null) return;
        running = StartCoroutine(Fade(atBlack, onDone));
    }

    IEnumerator Fade(Action atBlack, Action onDone)
    {
        yield return Ramp(0f, 1f);
        atBlack?.Invoke();
        yield return Ramp(1f, 0f);
        running = null;
        onDone?.Invoke();
    }

    IEnumerator Ramp(float from, float to)
    {
        float t = 0f;
        while (t < halfDurationSeconds)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / halfDurationSeconds)));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float alpha)
    {
        var c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
