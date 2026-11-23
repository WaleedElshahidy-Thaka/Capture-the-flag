using UnityEngine;
using UnityEngine.VFX;

// Plays a VFX Graph effect (e.g. one of the Adjustable Slash prefabs) for Grapple Field.
//
//   Grapple turns on   -> play
//   while it's on      -> play again every Repeat Every seconds (one-shot slashes keep coming)
//   Grapple steals     -> an extra play the moment a near-miss takes the Crown
//   Grapple ends       -> stop
//
// Follows GrappleField.IsActive, which is networked, so everyone sees it. Visual only.
// Put it on the car root (next to GrappleField).
public class GrappleFieldFX : MonoBehaviour
{
    [SerializeField] GrappleField grapple;
    [Tooltip("The Visual Effect on the car (child of View, so it moves smoothly with the robot). " +
             "Untick its 'Initial Event Name' play-on-awake, or leave it - it's stopped at start.")]
    [SerializeField] VisualEffect effect;

    [Header("Timing")]
    [Tooltip("While Grapple is on, play the effect again every this many seconds. 0 = play once on activation " +
             "(use 0 for an effect that loops by itself).")]
    [SerializeField, Min(0f)] float repeatEvery = 0.6f;
    [Tooltip("Extra play the moment a Grapple near-miss steals the Crown.")]
    [SerializeField] bool playOnSteal = true;

    [Header("Events (match the VFX Graph)")]
    [SerializeField] string playEvent = "OnPlay";
    [SerializeField] string stopEvent = "OnStop";

    bool wasActive;
    bool wasHolder;
    float nextPlay;

    void Awake()
    {
        if (grapple == null) grapple = GetComponent<GrappleField>();
        if (effect != null) effect.Stop(); // nothing on screen until Grapple is used
    }

    void LateUpdate()
    {
        if (effect == null) return;

        bool active = grapple != null && grapple.Object != null && grapple.Object.IsValid && grapple.IsActive;

        if (active && !wasActive)
        {
            Play();
            nextPlay = Time.time + repeatEvery;
        }
        else if (!active && wasActive)
        {
            effect.SendEvent(stopEvent);
        }
        else if (active && repeatEvery > 0f && Time.time >= nextPlay)
        {
            Play();
            nextPlay = Time.time + repeatEvery;
        }

        // A Grapple steal: this car becomes the holder while Grapple is on.
        var crown = CrownMatchState.Instance;
        bool holder = crown != null && grapple != null && grapple.Object != null && crown.IsHolder(grapple.Object);
        if (playOnSteal && active && holder && !wasHolder) Play();

        wasActive = active;
        wasHolder = holder;
    }

    void Play() => effect.SendEvent(playEvent);
}
