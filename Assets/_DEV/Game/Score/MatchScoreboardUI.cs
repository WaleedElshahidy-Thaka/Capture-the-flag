using System.Collections.Generic;
using UnityEngine;

// Live scoreboard, shown only while the match is being played (hidden in the lobby and after
// the final whistle). IMGUI so it needs no prefab or canvas setup - drop it on any scene object.
// Swap for your own UI later; it only reads MatchTimerState / PlayerRoster / CrownMatchState.
public class MatchScoreboardUI : MonoBehaviour
{
    static readonly List<PlayerMatchState> sorted = new List<PlayerMatchState>();

    [SerializeField] float width = 240f;
    [SerializeField] float margin = 16f;

    GUIStyle label;
    GUIStyle header;

    void OnGUI()
    {
        var timer = MatchTimerState.Instance;
        if (timer == null || !timer.IsPlaying) return;

        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            header = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
        }

        sorted.Clear();
        var players = PlayerRoster.Active;
        for (int i = 0; i < players.Count; i++)
            if (players[i] != null && players[i].HasState) sorted.Add(players[i]);
        sorted.Sort((a, b) => b.Score.CompareTo(a.Score));

        var crown = CrownMatchState.Instance;
        int seconds = Mathf.CeilToInt(timer.SecondsRemaining);

        GUILayout.BeginArea(new Rect(Screen.width - width - margin, margin, width, Screen.height - margin * 2f));
        GUILayout.Label($"{seconds / 60}:{seconds % 60:00}", header);

        int botNumber = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            var p = sorted[i];
            string name = p.IsBot ? "Bot " + (++botNumber) : p.DisplayName.ToString();
            if (string.IsNullOrEmpty(name)) name = "Player";
            if (p.Object.HasInputAuthority) name += " (you)";

            string holder = crown != null && crown.IsHolder(p.Object) ? " [CROWN]" : "";
            GUILayout.Label($"{name}{holder}: {p.Score}", label);
        }

        GUILayout.EndArea();
    }
}