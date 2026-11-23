using UnityEngine;

// Oil Slick patch: the dissolve sound as the patch ends. (The drop SPLAT uses OilPatch's own Drop Audio slot.)
//
// Plays once when the patch has this many seconds left - by default the clip's length (1.3 s), so the
// sound finishes just as the patch despawns (the AudioSource goes with it), overlapping its 1 s shrink. Reads OilPatch's networked age, so every peer hears it.
// Audio only - no networking of its own. Put it on the OilPatch root.
public class OilPatchAudio : MonoBehaviour
{
    [SerializeField] OilPatch patch;
    [SerializeField] AudioSource source;
    [SerializeField] AudioClip dissolveClip;
    [SerializeField, Range(0f, 1f)] float dissolveVolume = 0.7f;
    [Tooltip("Start the dissolve sound when the patch has this many seconds left. Keep it >= the clip length, " +
             "or the end is cut off when the patch despawns.")]
    [SerializeField, Min(0f)] float startSecondsBeforeEnd = 1.3f;

    bool played;

    void Awake()
    {
        if (patch == null) patch = GetComponent<OilPatch>();
        if (source != null) source.playOnAwake = false;
    }

    void Update()
    {
        if (played || patch == null || !patch.HasState || patch.Runner == null) return;

        float secondsLeft = (patch.LifetimeTicks - patch.AgeTicks) * patch.Runner.DeltaTime;
        if (secondsLeft > startSecondsBeforeEnd) return;

        played = true;
        if (source != null && dissolveClip != null)
        {
            source.pitch = Random.Range(0.95f, 1.05f);
            source.PlayOneShot(dissolveClip, dissolveVolume);
        }
    }
}
