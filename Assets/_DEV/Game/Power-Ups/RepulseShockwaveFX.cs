using UnityEngine;

// Repulse Pulse shockwave (CTF/RepulseShockwave on a flat quad). Plays ONCE each time the pulse fires:
// waves leave the car's outline and travel out to the push radius, then the renderer switches off.
//
// Networking: adds nothing. Listens to RepulsePulse.Fired, which every peer raises from Render when
// the replicated PulseCount changes, and sizes itself from the replicated RepulsePulse.Radius.
//
// Put it on the car root (next to RepulsePulse). The quad is a child of View, at the robot's feet,
// rotated 90 deg on X so it lies flat (its local Y = car forward).
public class RepulseShockwaveFX : MonoBehaviour
{
    [SerializeField] RepulsePulse pulse;
    [Tooltip("Flat quad with the RepulseShockwave material, child of View at ground level. No collider.")]
    [SerializeField] Renderer wave;
    [Tooltip("The car body. The waves start from this outline. Defaults to the BoxCollider on this object.")]
    [SerializeField] BoxCollider body;

    [Header("Look")]
    [SerializeField, Min(0.1f)] float duration = 0.6f;
    [Tooltip("Extra metres around the body outline where the waves start.")]
    [SerializeField, Min(0f)] float outlineMargin = 0.05f;
    [Tooltip("Roundness of the outline's corners, in metres.")]
    [SerializeField, Min(0f)] float cornerRadius = 0.25f;

    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int ExtentId = Shader.PropertyToID("_Extent");
    static readonly int BoxHalfId = Shader.PropertyToID("_BoxHalf");
    static readonly int CornerId = Shader.PropertyToID("_Corner");
    static readonly int MaxDistId = Shader.PropertyToID("_MaxDist");

    Material mat;       // per-car instance: keeps the SRP Batcher on
    float progress = 1f;

    void Awake()
    {
        if (pulse == null) pulse = GetComponent<RepulsePulse>();
        if (body == null) body = GetComponent<BoxCollider>();
        if (wave != null)
        {
            mat = wave.material;
            wave.enabled = false;
        }
    }

    void OnEnable() => RepulsePulse.Fired += OnFired;
    void OnDisable() => RepulsePulse.Fired -= OnFired;

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    void OnFired(RepulsePulse firedBy)
    {
        if (firedBy != pulse || wave == null || mat == null) return;

        Vector2 half = BodyHalfSize() + Vector2.one * outlineMargin;
        float radius = pulse.Radius;

        // Wave travels from the outline until its round far side reaches the push radius.
        float travel = Mathf.Max(0.1f, radius - Mathf.Min(half.x, half.y));
        float extent = radius + 0.5f; // room for the wave's edge width and wobble

        wave.transform.localScale = new Vector3(extent * 2f, extent * 2f, 1f);
        mat.SetFloat(ExtentId, extent);
        mat.SetVector(BoxHalfId, new Vector4(half.x, half.y, 0f, 0f));
        mat.SetFloat(CornerId, Mathf.Min(cornerRadius, Mathf.Min(half.x, half.y)));
        mat.SetFloat(MaxDistId, travel);

        progress = 0f;
        mat.SetFloat(ProgressId, 0f);
        wave.enabled = true;
    }

    void LateUpdate()
    {
        if (progress >= 1f || wave == null) return;

        progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
        mat.SetFloat(ProgressId, progress);

        if (progress >= 1f) wave.enabled = false;
    }

    // Body footprint in metres: x = width, y = length (car forward).
    Vector2 BodyHalfSize()
    {
        if (body == null) return new Vector2(0.45f, 0.55f);
        Vector3 s = body.transform.lossyScale;
        return new Vector2(body.size.x * s.x, body.size.z * s.z) * 0.5f;
    }
}
