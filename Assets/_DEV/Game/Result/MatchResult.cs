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
    public int slot;                 // seat, for the seat colour on the results board (SeatPalette)
    public float possessionSeconds;  // total time holding the Crown
    public float longestHoldSeconds; // longest single possession
    public int steals;
    public int slams;
    public float distanceMeters;
}

// Final standings of one match, built from the replicated car state on each peer, so every peer
// shows the same board.
[Serializable]
public class MatchResult
{
    public string savedAtUtc;
    public List<MatchResultEntry> entries = new List<MatchResultEntry>(); // final order, winner first
    public bool isDraw;          // nothing separates the top two (or nobody scored)
    public string winnerName;    // empty on a draw
    public bool wonOnTieBreak;   // top two level on points, separated by possession (FD-01)
    public int localScore;
    public int localRank;        // 1-based; 0 if the local player isn't in the list
    public bool localWon;
    public int localPointsOffWin; // winner's score minus yours (0 if you won, or lost on a tie-break)

    // Superlatives (FD-01): index into entries of each award's holder, -1 if nobody earned it.
    // Ties go to the higher-placed player.
    public int longestHoldIndex = -1;
    public int mostStealsIndex = -1;
    public int mostSlamsIndex = -1;
    public int mostDistanceIndex = -1;

    public static MatchResult Build()
    {
        var result = new MatchResult { savedAtUtc = DateTime.UtcNow.ToString("o") };
        var players = PlayerRoster.Active;
        int botNumber = 0;
        var counted = new HashSet<Fusion.NetworkId>(); // each car once, whatever the roster holds

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null || !p.HasState || p.Object == null || !p.Object.IsValid) continue;
            if (!counted.Add(p.Object.Id)) continue;

            string name = p.DisplayName.ToString();
            if (p.IsBot) name = "Bot " + (++botNumber);
            else if (string.IsNullOrEmpty(name)) name = "Player";

            float tickRate = Mathf.Max(1, p.Runner.TickRate);
            result.entries.Add(new MatchResultEntry
            {
                name = name,
                score = p.Score,
                isBot = p.IsBot,
                isLocal = p.Object.HasInputAuthority,
                slot = p.Slot,
                possessionSeconds = p.CrownTicks / tickRate,
                longestHoldSeconds = p.LongestHoldTicks / tickRate,
                steals = p.Steals,
                slams = p.Slams,
                distanceMeters = p.DistanceDriven,
            });
        }

        // FD-01: score, then total possession time, then longest single possession; seat last, so
        // every peer lists exact ties the same way.
        result.entries.Sort(Compare);

        if (result.entries.Count > 0)
        {
            var first = result.entries[0];
            var second = result.entries.Count > 1 ? result.entries[1] : null;

            result.isDraw = first.score == 0 || (second != null && Tied(first, second));
            result.wonOnTieBreak = !result.isDraw && second != null && second.score == first.score;
            result.winnerName = result.isDraw ? string.Empty : first.name;

            for (int i = 0; i < result.entries.Count; i++)
            {
                var e = result.entries[i];
                if (!e.isLocal) continue;
                result.localScore = e.score;
                result.localRank = i + 1;
                result.localWon = !result.isDraw && i == 0;
                result.localPointsOffWin = Mathf.Max(0, first.score - e.score);
                break;
            }
        }

        result.longestHoldIndex = BestIndex(result.entries, e => e.longestHoldSeconds);
        result.mostStealsIndex = BestIndex(result.entries, e => e.steals);
        result.mostSlamsIndex = BestIndex(result.entries, e => e.slams);
        result.mostDistanceIndex = BestIndex(result.entries, e => e.distanceMeters);

        return result;
    }

    static int Compare(MatchResultEntry a, MatchResultEntry b)
    {
        if (a.score != b.score) return b.score.CompareTo(a.score);
        if (!Mathf.Approximately(a.possessionSeconds, b.possessionSeconds)) return b.possessionSeconds.CompareTo(a.possessionSeconds);
        if (!Mathf.Approximately(a.longestHoldSeconds, b.longestHoldSeconds)) return b.longestHoldSeconds.CompareTo(a.longestHoldSeconds);
        return a.slot.CompareTo(b.slot);
    }

    // Level on every tie-break: a genuine draw.
    static bool Tied(MatchResultEntry a, MatchResultEntry b) =>
        a.score == b.score &&
        Mathf.Approximately(a.possessionSeconds, b.possessionSeconds) &&
        Mathf.Approximately(a.longestHoldSeconds, b.longestHoldSeconds);

    // Highest value wins; entries are already in final order, so a tie goes to the higher place.
    static int BestIndex(List<MatchResultEntry> entries, Func<MatchResultEntry, float> value)
    {
        int best = -1;
        float bestValue = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            float v = value(entries[i]);
            if (v > bestValue) { bestValue = v; best = i; }
        }
        return best;
    }
}

// Each client keeps its own copy, so the result survives the session closing.
public static class MatchResultsStore
{
    const string FileName = "last_match.json";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static MatchResult Last { get; private set; }

    // A new match is starting: forget the previous result, so nothing can show the old board.
    public static void Clear() => Last = null;

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