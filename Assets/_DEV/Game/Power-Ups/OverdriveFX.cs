using UnityEngine;

// Boost particles (e.g. "phantom dash", child of View so they move with the smoothed robot).
// Covers every boost: drift stage boosts and the Overdrive power-up (it fires a boost too).
//
//   Boost starts          -> play
//   New boost while on    -> replay (next drift stage, a chain, or Overdrive on top)
//   Boost ends            -> stop emitting; particles already out fade away on their own
//
// Follows PlayerMovement.IsBoosting and LastBoostTick, both from networked state, so everyone sees it.
// Visual only - no networking of its own. Put it on the car root (next to PlayerMovement).
public class OverdriveFX : MonoBehaviour
{
    [SerializeField] PlayerMovement movement;
    [Tooltip("The boost particle system. Untick its Play On Awake (it's also stopped at start).")]
    [SerializeField] ParticleSystem effect;

    bool wasActive;
    int lastBoostTick;

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void LateUpdate()
    {
        if (effect == null || movement == null || movement.Object == null || !movement.Object.IsValid) return;

        bool active = movement.IsBoosting;
        int boostTick = movement.LastBoostTick;

        if (active && (!wasActive || boostTick != lastBoostTick))
            Replay();
        else if (!active && wasActive)
            effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        wasActive = active;
        lastBoostTick = boostTick;
    }

    // Restart from the beginning; particles already out keep fading, so a stage-up reads as a fresh burst.
    void Replay()
    {
        effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        effect.Play(true);
    }
}
