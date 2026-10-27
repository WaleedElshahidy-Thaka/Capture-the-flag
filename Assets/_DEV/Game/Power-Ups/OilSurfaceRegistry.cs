using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Every live Oil Slick patch on this peer, keyed by runner so the multi-peer editor setup and the
// host-migration runner swap never mix sessions. OilPatch registers itself; the drive model only reads.
//
// Patches add no colliders: oil changes which surface a wheel probe resolves, never suspension
// height, wall detection or contact physics.
public static class OilSurfaceRegistry
{
    static readonly Dictionary<NetworkRunner, List<OilPatch>> byRunner =
        new Dictionary<NetworkRunner, List<OilPatch>>();

    internal static void Register(NetworkRunner runner, OilPatch patch)
    {
        if (runner == null || patch == null) return;
        if (!byRunner.TryGetValue(runner, out var list))
            byRunner[runner] = list = new List<OilPatch>();
        if (!list.Contains(patch)) list.Add(patch);
    }

    internal static void Unregister(NetworkRunner runner, OilPatch patch)
    {
        if (runner == null || !byRunner.TryGetValue(runner, out var list)) return;
        list.Remove(patch);
        if (list.Count == 0) byRunner.Remove(runner);
    }

    // Called four times per car per tick (once per grounded probe). Index loop, no allocation.
    public static bool IsOilAt(NetworkRunner runner, Vector3 groundPoint)
    {
        if (!byRunner.TryGetValue(runner, out var list)) return false;
        for (int i = 0; i < list.Count; i++)
        {
            OilPatch patch = list[i];
            if (patch != null && patch.HasState && patch.IsActive && patch.Contains(groundPoint))
                return true;
        }
        return false;
    }

    // Oldest live patch (highest age, ties by lowest NetworkId), for the active-patch cap.
    internal static OilPatch FindOldest(NetworkRunner runner, out int count)
    {
        count = 0;
        if (runner == null || !byRunner.TryGetValue(runner, out var list)) return null;

        OilPatch oldest = null;
        for (int i = 0; i < list.Count; i++)
        {
            OilPatch patch = list[i];
            if (patch == null || !patch.HasState) continue;
            count++;
            if (oldest == null
                || patch.AgeTicks > oldest.AgeTicks
                || (patch.AgeTicks == oldest.AgeTicks && patch.Object.Id.Raw < oldest.Object.Id.Raw))
                oldest = patch;
        }
        return oldest;
    }

    // Survives Enter Play Mode with domain reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => byRunner.Clear();
}
