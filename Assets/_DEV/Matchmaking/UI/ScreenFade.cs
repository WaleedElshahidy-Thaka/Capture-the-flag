using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Full-screen fade to black and back, running one action while the screen is black.
// Used for the cut from the solo view to the lobby view.
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

    // Ignored while a fade is already running.
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
