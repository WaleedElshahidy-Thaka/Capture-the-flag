using UnityEngine;

// Result of one suspension raycast.
public struct ProbeResult
{
    public bool HasContact;
    public float Compression;         // 0 at rest height, +1 fully compressed, negative hanging below rest
    public float CompressionVelocity; // positive while compressing
    public Vector3 GroundNormal;
    public Vector3 ContactPoint;
}

// Four raycasts from the wheel positions: answers "where is the ground under each corner".
// Knows nothing about driving. Probe origins are fixed local offsets baked from the art, not
// the tire transforms (those are animated every frame and would make the probes wobble).
public static class GroundProbes
{
    // offsets are ordered front-left, front-right, rear-left, rear-right.
    // Fills results in place so it can run every tick without allocating.
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
        // Signed around rest height, so the spring also pulls down when a wheel hangs below it.
        result.Compression = restLength > 0f
            ? Mathf.Clamp((restLength - hit.distance) / restLength, -1f, 1f)
            : 0f;
        // Derived from current body motion (not last tick's compression) so there's no extra state to roll back.
        result.CompressionVelocity = Vector3.Dot(body.GetPointVelocity(origin), -down);

        return result;
    }

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

    // Averaged (not summed) spring + damper response, so a chassis over a hole is supported in
    // proportion to how much of it is over ground.
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
