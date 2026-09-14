using UnityEngine;

// Step 2 of the Vehicle Drive Model's per-tick update (GDD doc 03): four raycasts from the
// chassis corners, resolving ride height, grounded state and ground normal. Nothing here knows
// anything about driving - it answers "where is the ground under each corner" and stops.
//
// Deliberately cast from authored corner offsets rather than from the visual tire transforms:
// those get rotated every frame by the cosmetic steer/roll animation, which would make the probe
// origins wobble and the whole simulation frame-rate dependent through the back door.
//
// Surface tags are not sampled. Track Data isn't being built and Glowtag FD-06 authors its own
// arena, where untagged geometry resolves as Default and is fully drivable - so there is nothing
// to sample yet. The probe result carries the normal, which is what the slope model (M3) needs.
public struct ProbeResult
{
    public bool HasContact;
    public float Compression;         // 0 at full extension, 1 fully compressed
    public float CompressionVelocity; // positive while compressing
    public Vector3 GroundNormal;
    public Vector3 ContactPoint;
}

public static class GroundProbes
{
    // Probe origins are supplied by the caller as local offsets, ordered front-left,
    // front-right, rear-left, rear-right. They're measured from the art once at prefab build
    // time (GameSceneSetup.WireTires) rather than computed from a guessed chassis box.
    //
    // Fills results in place rather than allocating, so this can run every tick without churn.
    public static void Cast(Transform chassis, Rigidbody body, Vector3[] offsets, float probeLength,
                            float restLength, LayerMask groundMask, ProbeResult[] results)
    {
        Vector3 down = -chassis.up;

        for (int i = 0; i < offsets.Length && i < results.Length; i++)
        {
            Vector3 origin = chassis.TransformPoint(offsets[i]);
            results[i] = CastOne(origin, down, body, probeLength, restLength, groundMask);
        }
    }

    static ProbeResult CastOne(Vector3 origin, Vector3 down, Rigidbody body, float probeLength,
                               float restLength, LayerMask groundMask)
    {
        var result = new ProbeResult();

        if (!Physics.Raycast(origin, down, out var hit, probeLength, groundMask, QueryTriggerInteraction.Ignore))
            return result;

        result.HasContact = true;
        result.GroundNormal = hit.normal;
        result.ContactPoint = hit.point;
        result.Compression = restLength > 0f
            ? Mathf.Clamp01((restLength - hit.distance) / restLength)
            : 0f;

        // Derived from the body's current motion at this point rather than from a remembered
        // previous compression. Same value, but it holds no state across ticks - so there is
        // nothing extra to replicate or restore on a rollback (doc 03's determinism contract).
        result.CompressionVelocity = Vector3.Dot(body.GetPointVelocity(origin), -down);

        return result;
    }

    // Grounded requires two or more probes in contact, per doc 03: a chassis clipping a kerb
    // with one corner is not grounded and does not get full traction from it.
    public static int CountGrounded(ProbeResult[] results)
    {
        int count = 0;
        for (int i = 0; i < results.Length; i++)
            if (results[i].HasContact) count++;
        return count;
    }

    public static Vector3 AverageGroundedNormal(ProbeResult[] results, Vector3 fallback)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < results.Length; i++)
        {
            if (!results[i].HasContact) continue;
            sum += results[i].GroundNormal;
            count++;
        }

        return count > 0 ? (sum / count).normalized : fallback;
    }

    // Averaged spring response across grounded probes. Averaged rather than summed so that
    // adding probes doesn't multiply the force, and so a chassis straddling a hole is supported
    // proportionally to how much of it is actually over ground.
    public static float SuspensionResponse(ProbeResult[] results, float springStrength, float damperStrength)
    {
        float total = 0f;
        int count = 0;

        for (int i = 0; i < results.Length; i++)
        {
            if (!results[i].HasContact) continue;
            total += springStrength * results[i].Compression - damperStrength * results[i].CompressionVelocity;
            count++;
        }

        return count > 0 ? total / count : 0f;
    }
}
