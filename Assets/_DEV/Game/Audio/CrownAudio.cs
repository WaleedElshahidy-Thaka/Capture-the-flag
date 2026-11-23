using Fusion;
using UnityEngine;

// Crown sounds, heard from each player's own point of view.
//
//   This car gets a free Crown (no holder -> this car, incl. match start) -> Pickup
//   This car takes the Crown from another car (A -> this car)          -> Steal
//   The local player's car loses the Crown to another car               -> Lost (only that player hears it)
//
// The local player hears their own Pickup/Steal/Lost full and non-positional; everyone else hears another
// car's Pickup/Steal in 3D from that car. Watches CrownMatchState.HolderId (networked) - no networking of
// its own, no gameplay change. Put it on the car root.
public class CrownAudio : MonoBehaviour
{
    [SerializeField] PlayerMatchState player;
    [SerializeField] AudioSource source;

    [Header("Clips")]
    [SerializeField] AudioClip pickupClip;
    [SerializeField] AudioClip stealClip;
    [SerializeField] AudioClip lostClip;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] float pickupVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] float stealVolume = 1f;
    [SerializeField, Range(0f, 1f)] float lostVolume = 0.8f;

    /// <summary>Raised when a steal stinger plays (e.g. robots duck their drive loops under it).</summary>
    public static event System.Action StealStingerPlayed;

    bool initialized;
    NetworkId lastHolder;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerMatchState>();
        if (source != null) source.playOnAwake = false;
    }

    void Update()
    {
        var crown = CrownMatchState.Instance;
        if (crown == null || crown.Object == null || !crown.Object.IsValid) return;
        if (player == null || player.Object == null || !player.Object.IsValid) return;

        NetworkId holder = crown.HolderId;

        // First valid frame (spawn / join mid-match): take the current holder, play nothing.
        if (!initialized)
        {
            initialized = true;
            lastHolder = holder;
            return;
        }
        if (holder == lastHolder) return;

        NetworkId me = player.Object.Id;
        bool isLocal = player == PlayerRoster.Local;

        if (holder == me)
        {
            bool stolen = lastHolder != default;     // A -> me is a steal; nobody -> me is a pickup
            Play(stolen ? stealClip : pickupClip, stolen ? stealVolume : pickupVolume, isLocal);
            if (stolen) StealStingerPlayed?.Invoke();
        }
        else if (lastHolder == me && holder != default && isLocal)
        {
            Play(lostClip, lostVolume, true);          // only the player who lost it (not the match-end reset)
        }

        lastHolder = holder;
    }

    void Play(AudioClip clip, float volume, bool nonPositional)
    {
        if (source == null || clip == null) return;
        source.spatialBlend = nonPositional ? 0f : 1f;
        source.PlayOneShot(clip, volume);
    }
}
