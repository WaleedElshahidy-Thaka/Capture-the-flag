using UnityEngine;

// The power-up vase breaking and coming back (visual only).
//
//   Picked up (PowerUpCrate.IsShown -> false) -> the whole vase hides, its Broken pieces burst outward,
//                                               bounce on the floor and shrink away; a small burst plays
//   Respawned (IsShown -> true)               -> the vase pops back in at the same spot, pieces reset
//
// Follows PowerUpCrate.IsShown, which is networked, so every peer sees the same break - no networking of
// its own. The pieces are animated by hand (no Rigidbodies), so they never touch or push the cars.
// Put it on the crate root, next to PowerUpCrate.
public class CrateBreakFX : MonoBehaviour
{
    [SerializeField] PowerUpCrate crate;
    [Tooltip("The intact vase (e.g. SM_Vase_D01/Fixed).")]
    [SerializeField] GameObject fixedVisual;
    [Tooltip("Parent of the broken pieces (e.g. SM_Vase_D01/Broken). Hidden until the vase breaks.")]
    [SerializeField] Transform brokenRoot;
    [Tooltip("One-shot burst played where the vase breaks (dust, sparks). Optional.")]
    [SerializeField] ParticleSystem[] bursts;

    [Header("Break")]
    [SerializeField, Min(0f)] float outwardSpeed = 3.2f;
    [SerializeField, Min(0f)] float upwardSpeed = 3.8f;
    [SerializeField, Min(0f)] float spinDegreesPerSecond = 540f;
    [SerializeField, Min(0f)] float gravity = 16f;
    [Tooltip("Speed kept after hitting the floor (0 = stops dead, 1 = no loss).")]
    [SerializeField, Range(0f, 1f)] float bounciness = 0.35f;
    [Tooltip("How long the pieces stay before they've fully shrunk away.")]
    [SerializeField, Min(0.1f)] float pieceLifetime = 1.5f;
    [SerializeField, Min(0.05f)] float shrinkSeconds = 0.4f;

    [Header("Respawn")]
    [SerializeField, Min(0f)] float popInSeconds = 0.35f;

    [Header("Sound")]
    [Tooltip("3D source on the crate for the break and respawn sounds.")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip breakClip;
    [SerializeField] AudioClip respawnClip;
    [SerializeField, Range(0f, 1f)] float breakVolume = 1f;
    [SerializeField, Range(0f, 1f)] float respawnVolume = 0.5f;
    [Tooltip("Random pitch +/- this much, so repeated smashes don't sound identical.")]
    [SerializeField, Range(0f, 0.3f)] float pitchVariation = 0.08f;

    struct Piece
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector3 velocity;     // world
        public Vector3 spinAxis;
        public float spinSpeed;
    }

    Piece[] pieces;
    Vector3 fixedScale = Vector3.one;
    bool initialized;
    bool wasShown;
    float breakAge = -1f;   // < 0 = not breaking
    float popAge = -1f;     // < 0 = not popping in

    void Awake()
    {
        if (crate == null) crate = GetComponent<PowerUpCrate>();
        if (fixedVisual != null) fixedScale = fixedVisual.transform.localScale;

        if (brokenRoot != null)
        {
            pieces = new Piece[brokenRoot.childCount];
            for (int i = 0; i < pieces.Length; i++)
            {
                Transform t = brokenRoot.GetChild(i);
                pieces[i] = new Piece
                {
                    transform = t,
                    localPosition = t.localPosition,
                    localRotation = t.localRotation,
                    localScale = t.localScale,
                };
            }
            brokenRoot.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (crate == null || crate.Object == null || !crate.Object.IsValid) return;

        bool shown = crate.IsShown;

        // First valid frame (spawn, or joining mid-match): show the current state, no break replay.
        if (!initialized)
        {
            initialized = true;
            wasShown = shown;
            SetFixed(shown, 1f);
            return;
        }

        if (wasShown && !shown) Break();
        else if (!wasShown && shown) Respawn();
        wasShown = shown;

        if (breakAge >= 0f) UpdatePieces(Time.deltaTime);
        if (popAge >= 0f) UpdatePopIn(Time.deltaTime);
    }

    void Break()
    {
        SetFixed(false, 1f);
        popAge = -1f;
        PlaySound(breakClip, breakVolume);

        if (bursts != null)
            foreach (var b in bursts) if (b != null) b.Play(true);

        if (pieces == null || brokenRoot == null) return;

        ResetPieces();
        brokenRoot.gameObject.SetActive(true);

        Vector3 centre = brokenRoot.position;
        for (int i = 0; i < pieces.Length; i++)
        {
            Vector3 away = pieces[i].transform.position - centre;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Random.insideUnitSphere.normalized;

            pieces[i].velocity = away * outwardSpeed * Random.Range(0.7f, 1.25f)
                               + Vector3.up * upwardSpeed * Random.Range(0.6f, 1.2f);
            pieces[i].spinAxis = Random.onUnitSphere;
            pieces[i].spinSpeed = spinDegreesPerSecond * Random.Range(0.5f, 1.2f);
        }
        breakAge = 0f;
    }

    void Respawn()
    {
        EndBreak();
        PlaySound(respawnClip, respawnVolume);
        SetFixed(true, 0f);
        popAge = popInSeconds > 0f ? 0f : -1f;
        if (popAge < 0f) SetFixed(true, 1f);
    }

    void UpdatePieces(float dt)
    {
        breakAge += dt;
        // The vase's own root sits on the floor (the crate root may float above it).
        float groundY = brokenRoot.parent != null ? brokenRoot.parent.position.y : transform.position.y;
        float shrink = Mathf.Clamp01((pieceLifetime - breakAge) / shrinkSeconds);

        for (int i = 0; i < pieces.Length; i++)
        {
            ref Piece p = ref pieces[i];
            p.velocity += Vector3.down * gravity * dt;

            Vector3 pos = p.transform.position + p.velocity * dt;
            if (pos.y < groundY && p.velocity.y < 0f)
            {
                pos.y = groundY;
                p.velocity.y = -p.velocity.y * bounciness;
                p.velocity.x *= 0.7f;    // floor friction
                p.velocity.z *= 0.7f;
                p.spinSpeed *= 0.6f;
            }

            p.transform.position = pos;
            p.transform.Rotate(p.spinAxis, p.spinSpeed * dt, Space.World);
            p.transform.localScale = p.localScale * shrink;
        }

        if (breakAge >= pieceLifetime) EndBreak();
    }

    void EndBreak()
    {
        breakAge = -1f;
        if (brokenRoot != null) brokenRoot.gameObject.SetActive(false);
        ResetPieces();
    }

    void ResetPieces()
    {
        if (pieces == null) return;
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].transform.localPosition = pieces[i].localPosition;
            pieces[i].transform.localRotation = pieces[i].localRotation;
            pieces[i].transform.localScale = pieces[i].localScale;
            pieces[i].velocity = Vector3.zero;
        }
    }

    void UpdatePopIn(float dt)
    {
        popAge += dt;
        float t = Mathf.Clamp01(popAge / popInSeconds);
        SetFixed(true, EaseOutBack(t));
        if (t >= 1f) popAge = -1f;
    }

    void SetFixed(bool visible, float scale)
    {
        if (fixedVisual == null) return;
        if (fixedVisual.activeSelf != visible) fixedVisual.SetActive(visible);
        fixedVisual.transform.localScale = fixedScale * Mathf.Max(0.001f, scale);
    }

    void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null) return;
        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip, volume);
    }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }
}
