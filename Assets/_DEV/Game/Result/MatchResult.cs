using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class MatchResultEntry
{
    public string name;
    public int score;
    public bool isBot;
    public bool isLocal;
}

// Final standings of one match, built from the replicated car state on each peer.
[Serializable]
public class MatchResult
{
    public string savedAtUtc;
    public List<MatchResultEntry> entries = new List<MatchResultEntry>(); // highest score first
    public bool isDraw;       // top score shared, or nobody scored
    public string winnerName; // empty on a draw
    public int localScore;
    public bool localWon;

    public static MatchResult Build()
    {
        var result = new MatchResult { savedAtUtc = DateTime.UtcNow.ToString("o") };
        var players = PlayerRoster.Active;
        int botNumber = 0;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null || !p.HasState) continue;

            string name = p.DisplayName.ToString();
            if (p.IsBot) name = "Bot " + (++botNumber);
            else if (string.IsNullOrEmpty(name)) name = "Player";

            result.entries.Add(new MatchResultEntry
            {
                name = name,
                score = p.Score,
                isBot = p.IsBot,
                isLocal = p.Object.HasInputAuthority
            });
        }

        result.entries.Sort((a, b) => b.score.CompareTo(a.score));

        if (result.entries.Count > 0)
        {
            int top = result.entries[0].score;
            result.isDraw = top == 0 || (result.entries.Count > 1 && result.entries[1].score == top);
            result.winnerName = result.isDraw ? string.Empty : result.entries[0].name;

            for (int i = 0; i < result.entries.Count; i++)
            {
                if (!result.entries[i].isLocal) continue;
                result.localScore = result.entries[i].score;
                result.localWon = !result.isDraw && i == 0;
                break;
            }
        }

        return result;
    }
}

// Each client keeps its own copy, so the result survives the session closing.
public static class MatchResultsStore
{
    const string FileName = "last_match.json";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static MatchResult Last { get; private set; }

    public static void Save(MatchResult result)
    {
        Last = result;
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(result, true));
            Debug.Log($"[Match] Result saved: {FilePath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Match] Could not save result: {e.Message}");
        }
    }

    public static MatchResult LoadLast()
    {
        if (Last != null) return Last;
        try
        {
            if (File.Exists(FilePath))
                Last = JsonUtility.FromJson<MatchResult>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Match] Could not load result: {e.Message}");
        }
        return Last;
    }
}