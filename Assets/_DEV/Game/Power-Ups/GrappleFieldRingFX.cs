using Fusion;
using UnityEngine;

// Grapple Field ground ring (CTF/GrappleRing shader on a flat quad). It shows the steal reach around
// the robot while Grapple is on: waves pulse out and in, and the ring turns to the Alert colour when
// the Crown holder is inside the reach.
//
//   Grapple turns on  -> ring grows out from the robot
//   Holder in reach   -> ring turns hot and pulses (GrappleField.IsInStealRange, the same check the steal uses)
//   Last second       -> flickers, so the holder knows it's about to end
//   Grapple ends      -> ring shrinks away, renderer switched off (zero cost while idle)
//
// Networking: adds nothing. It follows GrappleField.IsActive (already [Networked]) and the replicated
// Crown HolderId. The highlight is cosmetic; the State Authority still decides every steal.
//
// Put it on the car root (next to GrappleField). The ring is a child of View, so it moves with the
// smoothed robot.
public class GrappleFieldRingFX : MonoBehaviour
{
    [SerializeField] GrappleField grapple;
    [Tooltip("Flat quad with the GrappleRing material, child of View at the robot's feet. No collider.")]
    [SerializeField] Renderer ring;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] float growSeconds = 0.3f;
    [SerializeField, Min(0.01f)] float shrinkSeconds = 0.2f;
    [Tooltip("How fast the Alert colour blends in/out (per second).")]
    [SerializeField, Min(0.1f)] float alertBlendSpeed = 8f;

    [Header("Holder highlight")]
    [Tooltip("Turn the ring to the Alert colour while the Crown holder is within reach.")]
    [SerializeField] bool highlightHolderInReach = true;

    [Header("Last seconds")]
    [SerializeField] bool flickerBeforeEnd = true;
    [SerializeField, Min(0f)] float flickerSeconds = 1f;

    static readonly int RevealId = Shader.PropertyToID("_Reveal");
    static readonly int AlertId = Shader.PropertyToID("_Alert");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    Material mat;          // per-car instance: keeps the SRP Batcher on (no MaterialPropertyBlock)
    float baseIntensity = 1f;
    float shown;           // 0 hidden .. 1 fully grown
    float alert;           // 0 .. 1
    bool wasActive;
    float activeSince;
    float lastDiameter = -1f;

    // Cached holder lookup, refreshed only when the Crown changes hands.
    NetworkId cachedHolderId;
    BoxCollider cachedHolderBox;

    void Awake()
    {
        if (grapple == null) grapple = GetComponent<GrappleField>();
        if (ring != null)
        {
            mat = ring.material; // instance
            if (mat.HasProperty(IntensityId)) baseIntensity = mat.GetFloat(IntensityId);
            ring.enabled = false;
        }
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    void LateUpdate()
    {
        if (ring == null || mat == null) return;

        bool active = grapple != null && grapple.Object != null && grapple.Object.IsValid && grapple.IsActive;
        if (active && !wasActive) activeSince = Time.time;
        wasActive = active;

        shown = Mathf.MoveTowards(shown, active ? 1f : 0f, Time.deltaTime / (active ? growSeconds : shrinkSeconds));

        bool visible = shown > 0.001f;
        if (ring.enabled != visible) ring.enabled = visible;
        if (!visible) { alert = 0f; return; }

        UpdateSize();

        bool inReach = active && highlightHolderInReach && HolderInReach();
        alert = Mathf.MoveTowards(alert, inReach ? 1f : 0f, alertBlendSpeed * Time.deltaTime);

        float flicker = 1f;
        if (active && flickerBeforeEnd)
        {
            float left = HandlingValuesProvider.Current.grappleDurationSeconds - (Time.time - activeSince);
            if (left < flickerSeconds) flicker = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 25f));
        }

        mat.SetFloat(RevealId, EaseOutCubic(shown));
        mat.SetFloat(AlertId, alert);
        mat.SetFloat(IntensityId, baseIntensity * flicker);
    }

    // Ring edge = GrappleField.StealRadius, the exact circle the steal check uses.
    void UpdateSize()
    {
        float diameter = 2f * grapple.StealRadius;
        if (Mathf.Abs(diameter - lastDiameter) < 0.001f) return;

        lastDiameter = diameter;
        // The quad is rotated 90 deg on X to lie flat, so its local X/Y span the ground.
        ring.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    bool HolderInReach()
    {
        var crown = CrownMatchState.Instance;
        if (crown == null || !crown.HasHolder || grapple.Runner == null) return false;
        if (crown.IsHolder(grapple.Object)) return false; // we already hold it

        if (crown.HolderId != cachedHolderId || cachedHolderBox == null)
        {
            cachedHolderId = crown.HolderId;
            cachedHolderBox = grapple.Runner.TryFindObject(cachedHolderId, out NetworkObject holder) && holder != null
                ? holder.GetComponent<BoxCollider>()
                : null;
        }

        if (cachedHolderBox == null || !cachedHolderBox.enabled) return false;

        return grapple.IsInStealRange(cachedHolderBox);
    }

    static float EaseOutCubic(float k)
    {
        k = 1f - k;
        return 1f - k * k * k;
    }
}
