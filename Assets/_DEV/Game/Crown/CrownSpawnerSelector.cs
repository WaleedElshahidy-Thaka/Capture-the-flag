
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public static class CrownSpawnSelector
{
    public static Transform Select(
        Transform[] candidates,
        IReadOnlyList<PlayerMatchState> players,
        int tick,
        float minSpawnDistance,
        float initialAsymmetryRatio = 0.7f)
    {
        if (candidates == null || candidates.Length == 0)
        {
            Debug.LogError("[CrownSpawn] No authored spawn points assigned.");
            return null;
        }

        if (players == null || players.Count < 2)
        {
            Debug.LogError("[CrownSpawn] At least two participants are required.");
            return null;
        }

        float ratio = Mathf.Clamp(initialAsymmetryRatio, 0.55f, 1f);

        // Relax the requirement in 0.05 steps until a candidate qualifies.
        while (ratio <= 1.001f)
        {
            List<Transform> valid = new List<Transform>();

            foreach (Transform candidate in candidates)
            {
                if (candidate == null)
                    continue;

                if (IsValid(candidate.position, players,
                    minSpawnDistance, ratio))
                {
                    valid.Add(candidate);
                }
            }

            if (valid.Count > 0)
            {
                int seed = BuildSeed(tick, players);
                int index = (int)((uint)seed % (uint)valid.Count);

                Debug.Log(
                    $"[CrownSpawn] Selected {valid[index].name}; " +
                    $"ratio={ratio:F2}, candidates={valid.Count}, tick={tick}");

                return valid[index];
            }

            ratio += 0.05f;
        }

        // Last-resort fallback: still respect the minimum distance,
        // but accept a fairer spawn if no asymmetric candidate exists.
        List<Transform> fallback = new List<Transform>();

        foreach (Transform candidate in candidates)
        {
            if (candidate == null)
                continue;

            float nearest = GetNearestDistances(
                candidate.position, players, out _);

            if (nearest >= minSpawnDistance)
                fallback.Add(candidate);
        }

        if (fallback.Count == 0)
        {
            Debug.LogError(
                "[CrownSpawn] No candidate satisfies minimum spawn distance.");
            return null;
        }

        int fallbackSeed = BuildSeed(tick, players);
        int fallbackIndex =
            (int)((uint)fallbackSeed % (uint)fallback.Count);

        Debug.LogWarning(
            "[CrownSpawn] Used fair-spawn fallback; asymmetry requirement relaxed.");

        return fallback[fallbackIndex];
    }

    private static bool IsValid(
        Vector3 position,
        IReadOnlyList<PlayerMatchState> players,
        float minSpawnDistance,
        float ratio)
    {
        float nearest = GetNearestDistances(
            position, players, out float secondNearest);

        if (nearest == float.MaxValue ||
            secondNearest == float.MaxValue)
        {
            return false;
        }

        if (nearest < minSpawnDistance)
            return false;

        return nearest <= ratio * secondNearest;
    }

    private static float GetNearestDistances( Vector3 position, IReadOnlyList<PlayerMatchState> players, out float secondNearest)
    {
        float nearest = float.MaxValue;
        secondNearest = float.MaxValue;

        foreach (PlayerMatchState player in players)
        {
            if (player == null || !player.HasState || player.Object == null)
                continue;

            float distance = Vector3.Distance(
                position, player.transform.position);

            if (distance < nearest)
            {
                secondNearest = nearest;
                nearest = distance;
            }
            else if (distance < secondNearest)
            {
                secondNearest = distance;
            }
        }
        Debug.Log($"[point] Point= {position} ,[CrownSpawn] Nearest={nearest:F2}, SecondNearest={secondNearest:F2}");
        return nearest;
    }

    private static int BuildSeed( int tick, IReadOnlyList<PlayerMatchState> players)
    {
        unchecked
        {
            uint hash = (uint)tick;

            foreach (PlayerMatchState player in players)
            {
                if (player == null || !player.HasState || player.Object == null)
                    continue;

                hash ^= (uint)player.Object.InputAuthority.RawEncoded
                    + 0x9E3779B9u + (hash << 6) + (hash >> 2);

                // Distinguishes bots, whose InputAuthority is PlayerRef.None.
                hash ^= player.Object.Id.Raw
                    + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            }

            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;

            return (int)hash;
        }
    }
}
