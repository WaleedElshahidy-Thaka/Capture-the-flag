using UnityEngine;

// Lays tyre tracks under the chosen wheels while the car is drifting (PlayerMovement.IsSliding, which is
// networked, so every peer draws every car's tracks). A short raycast under each wheel puts the mark on
// the ground and stops marks in the air. All marks go into the shared SkidmarkSystem (one draw call).
//
// Visual only - no networking of its own. Put it on the car root (next to PlayerMovement). Does nothing
// while the car isn't drifting.
public class CarSkidmarks : MonoBehaviour
{
    [SerializeField] PlayerMovement movement;
    [Tooltip("Wheels that leave tracks (e.g. the two rear tyres, under View so they move smoothly).")]
    [SerializeField] Transform[] wheels;
    [Tooltip("CTF/Skidmark material (shared by every car).")]
    [SerializeField] Material material;

    [Header("Shape")]
    [SerializeField, Min(0.01f)] float markWidth = 0.2f;
    [Tooltip("A new segment every this many metres.")]
    [SerializeField, Min(0.02f)] float segmentLength = 0.1f;
    [Tooltip("Lift above the ground, against z-fighting.")]
    [SerializeField, Min(0f)] float groundOffset = 0.01f;

    [Header("Ground check")]
    [SerializeField] LayerMask groundMask = ~0;
    [Tooltip("How far below the wheel centre the ground may be and still count as touching.")]
    [SerializeField, Min(0.05f)] float groundProbe = 0.45f;

    Rigidbody body;
    SkidmarkSystem system;
    long[] lastSerial;
    Vector3[] lastPoint;
    float[] distance;
    readonly RaycastHit[] hits = new RaycastHit[8];

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        body = GetComponent<Rigidbody>();

        int n = wheels != null ? wheels.Length : 0;
        lastSerial = new long[n];
        lastPoint = new Vector3[n];
        distance = new float[n];
        for (int i = 0; i < n; i++) lastSerial[i] = -1;
    }

    void LateUpdate()
    {
        if (wheels == null || material == null) return;

        bool drifting = movement != null && movement.Object != null && movement.Object.IsValid && movement.IsSliding;

        for (int i = 0; i < wheels.Length; i++)
        {
            if (!drifting || wheels[i] == null || !TryGround(wheels[i], out Vector3 point, out Vector3 normal))
            {
                lastSerial[i] = -1; // the track ends; the next one starts fresh
                continue;
            }

            bool continues = lastSerial[i] >= 0;
            Vector3 step = point - lastPoint[i];
            if (continues && step.sqrMagnitude < segmentLength * segmentLength) continue;

            if (continues) distance[i] += step.magnitude;
            Vector3 direction = continues ? step : transform.forward; // car forward (tyre meshes may spin)

            system ??= SkidmarkSystem.Get(material);
            lastSerial[i] = system.Add(point + normal * groundOffset, normal, direction, markWidth * 0.5f,
                                       distance[i], lastSerial[i]);
            lastPoint[i] = point;
        }
    }

    // Nearest ground hit straight below the wheel, ignoring this car's own colliders and triggers.
    bool TryGround(Transform wheel, out Vector3 point, out Vector3 normal)
    {
        Vector3 up = transform.up;
        Vector3 origin = wheel.position + up * 0.2f;
        int count = Physics.RaycastNonAlloc(origin, -up, hits, 0.2f + groundProbe, groundMask, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        point = default;
        normal = up;
        for (int h = 0; h < count; h++)
        {
            Collider c = hits[h].collider;
            if (body != null && c.attachedRigidbody == body) continue;
            if (hits[h].distance < best)
            {
                best = hits[h].distance;
                point = hits[h].point;
                normal = hits[h].normal;
            }
        }
        return best < float.MaxValue;
    }
}
