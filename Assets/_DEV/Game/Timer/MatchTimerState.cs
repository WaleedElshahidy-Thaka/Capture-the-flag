using System;
using Fusion;
using UnityEngine;

// 90-second match clock + crown scoring. Lives on the CrownMatchState prefab, so it is spawned
// (by MatchStarter) only when a match actually starts and only on the host.
//
// Time is counted as REMAINING ticks that the host decrements, not as a start tick, so a host
// migration (which re-bases ticks) cannot restart or shorten the match. The clock and scoring
// both pause while MatchmakingSessionState.Frozen is set.
//
// Score: 1 point per second the crown is held, stored as ticks held on the holder's car
// (PlayerMatchState.CrownTicks). Banked instantly, never reduced, no multipliers.
[RequireComponent(typeof(CrownMatchState))]
public class MatchTimerState : NetworkBehaviour
{
    [HideInInspector]
    [SerializeField, Min(1f)] float durationSeconds = 90f;
    [SerializeField, Min(0f)] float pointsPerSecond = 0.6f; // flat rate, no multipliers. 0.5 = 1 point per 2 s

    // Declaration order is the network layout - keep it stable.
    [Networked] int RemainingTicks { get; set; }
    [Networked] NetworkBool Started { get; set; }
    [Networked] public NetworkBool Finished { get; set; }

    public static MatchTimerState Instance { get; private set; }

    // Raised once per peer when the match ends. The result is already saved locally.
    public static event Action<MatchResult> MatchFinished;

    public bool IsPlaying => Started && !Finished;

    public float SecondsRemaining =>
        Runner == null ? 0f : Mathf.Max(0f, RemainingTicks * Runner.DeltaTime);

    CrownMatchState crown;
    bool reported;

    void Awake()
    {
        crown = GetComponent<CrownMatchState>();
    }

    public override void Spawned()
    {
        Instance = this;

        // Spawned also runs for objects restored after a host migration - only start once.
        if (Object.HasStateAuthority && !Started)
        {
            RemainingTicks = Mathf.RoundToInt(durationSeconds * Runner.TickRate);
            Started = true;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    // Host only.
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !IsPlaying) return;

        var session = MatchmakingSessionState.Local;
        if (session != null && session.Frozen) return;

        if (crown.HolderId != default)
        {
            var players = PlayerRoster.Active;
            for (int i = 0; i < players.Count; i++)
            {
                var holder = players[i];
                if (holder.Object.Id != crown.HolderId) continue;

                // Flat rate: Score is whole points earned from total held time. CrownTicks carries
                // over between holds, so partial progress toward the next point is never lost.
                holder.CrownTicks++;
                holder.Score = (int)(holder.CrownTicks * (double)pointsPerSecond / Runner.TickRate);
                break;
            }
        }

        RemainingTicks--;
        if (RemainingTicks <= 0)
        {
            RemainingTicks = 0;
            Finish();
        }
    }

    void Finish()
    {
        Finished = true;

        // Stop every car through the existing movement gate (player and bot input both honour it).
        var players = PlayerRoster.Active;
        for (int i = 0; i < players.Count; i++)
            players[i].CanMove = false;

        Debug.Log("[Match] Time's up.");
    }

    // Every peer, including the host: scores and Finished arrive in the same snapshot.
    public override void Render()
    {
        if (!Finished || reported) return;
        reported = true;

        MatchResult result = MatchResult.Build();
        MatchResultsStore.Save(result);
        MatchFinished?.Invoke(result);
    }
}