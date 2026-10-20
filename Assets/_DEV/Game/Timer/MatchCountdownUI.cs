using UnityEngine;

// Big "3, 2, 1, GO!" over the screen while MatchTimerState's pre-match countdown runs.
// IMGUI like the other match UI, so it needs no canvas - drop it on any active scene object.
public class MatchCountdownUI : MonoBehaviour
{
    [SerializeField] float goSeconds = 0.8f;

    bool wasCountingDown;
    float goUntil;
    GUIStyle style;

    void Update()
    {
        var timer = MatchTimerState.Instance;
        bool counting = timer != null && timer.InCountdown;

        // Countdown just ended: flash GO!
        if (wasCountingDown && !counting && timer != null && timer.IsPlaying)
            goUntil = Time.unscaledTime + goSeconds;

        wasCountingDown = counting;
    }

    void OnGUI()
    {
        var timer = MatchTimerState.Instance;

        string text = null;
        if (timer != null && timer.InCountdown)
            text = Mathf.Max(1, Mathf.CeilToInt(timer.CountdownSecondsRemaining)).ToString();
        else if (Time.unscaledTime < goUntil)
            text = "GO!";

        if (text == null) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 120,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height * 0.7f), text, style);
    }
}
