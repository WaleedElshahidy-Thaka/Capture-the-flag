using System;
using UnityEngine;

// Surface tags the drive model understands. Untagged geometry resolves as Default and is fully
// drivable (FD-06). Oil exists only as runtime patches dropped by the Oil Slick power-up.
public enum SurfaceType : byte
{
    Default = 0,
    Oil = 1,
}

// Turns four probe contacts into one surface, and one surface into drive-model multipliers.
// Pure functions, no state - same shape as WallContact.
public static class SurfaceProbe
{
    // Steering authority is never zero, whatever a surface's tuning says (FD-04 hard constraint).
    const float MinSteerAuthority = 0.1f;

    // Majority over the grounded probes. Overlapping patches can't stack: a probe is either on oil
    // or not, so two patches under one wheel still count once (FD-04 edge case).
    // PROVISIONAL tie rule: 2 of 4 counts as Oil, so a car half on the patch still feels it.
    // Check against the Drive Model doc's majority rule and flip to a strict majority if it says so.
    public static SurfaceType Resolve(ProbeResult[] probes, Func<Vector3, SurfaceType> surfaceAt)
    {
        if (surfaceAt == null) return SurfaceType.Default;

        int grounded = 0;
        int oil = 0;
        for (int i = 0; i < probes.Length; i++)
        {
            if (!probes[i].HasContact) continue;
            grounded++;
            if (surfaceAt(probes[i].ContactPoint) == SurfaceType.Oil) oil++;
        }

        return oil > 0 && oil * 2 >= grounded ? SurfaceType.Oil : SurfaceType.Default;
    }

    public static float GripMultiplier(SurfaceType surface, HandlingValues h) =>
        surface == SurfaceType.Oil ? Mathf.Max(0f, h.OilGripMultiplier) : 1f;

    public static float SteerAuthority(SurfaceType surface, HandlingValues h) =>
        surface == SurfaceType.Oil ? Mathf.Max(MinSteerAuthority, h.OilSteerAuthority) : 1f;
}
