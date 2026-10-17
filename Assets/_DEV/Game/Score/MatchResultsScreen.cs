using UnityEngine;

// End-of-match results: final standings, win/draw headline and a Play Again button.
// Shown when MatchTimerState raises MatchFinished (the result is already saved locally by then).
// IMGUI like MatchScoreboardUI, so it needs no canvas setup - drop it on any active scene object
// that exists from the start of the scene (it must be listening before the match ends).
public class MatchResultsScreen : MonoBehaviour
{
    [SerializeField] MatchmakingFlowController flow;
    [SerializeField] float width = 440f;

    MatchResult result;
    GUIStyle titleStyle, rowStyle, buttonStyle;

    void OnEnable() => MatchTimerState.MatchFinished += OnFinished;
    void OnDisable() => MatchTimerState.MatchFinished -= OnFinished;

    void Start()
    {
        if (flow == null)
            flow = FindFirstObjectByType<MatchmakingFlowController>();

        MatchResult savedResult = MatchResultsStore.LoadLast();

        if (savedResult != null)
            result = savedResult;
    }

    void OnFinished(MatchResult finished) => result = finished;

    void OnGUI()
    {
        if (result == null) return;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        }

        var entries = result.entries;
        float height = 150f + entries.Count * 30f + 70f;
        var area = new Rect((Screen.width - width) / 2f, Mathf.Max(20f, (Screen.height - height) / 2f), width, height);

        GUI.Box(area, GUIContent.none);
        GUILayout.BeginArea(new Rect(area.x + 20f, area.y + 20f, area.width - 40f, area.height - 40f));

        string headline = result.isDraw ? "Draw!"
            : result.localWon ? "You win!"
            : result.winnerName + " wins";
        GUILayout.Label(headline, titleStyle);
        GUILayout.Space(12f);

        int rank = 1;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (i > 0 && e.score < entries[i - 1].score) rank = i + 1; // ties share a rank

            var previous = GUI.color;
            if (e.isLocal) GUI.color = new Color(1f, 0.85f, 0.2f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(rank + ".", rowStyle, GUILayout.Width(36f));
            GUILayout.Label(e.isLocal ? e.name + " (you)" : e.name, rowStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(e.score.ToString(), rowStyle, GUILayout.Width(60f));
            GUILayout.EndHorizontal();

            GUI.color = previous;
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Play Again", buttonStyle, GUILayout.Height(46f)))
        {
            result = null;
            if (flow != null) flow.PlayAgain();
            else Debug.LogWarning("[Match] No MatchmakingFlowController found for Play Again.");
        }

        GUILayout.EndArea();
    }
}
