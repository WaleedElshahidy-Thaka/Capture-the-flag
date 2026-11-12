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
    [SerializeField, Min(0f)] float countdownSeconds = 3f; // "3, 2, 1" before the clock starts
    // Declaration order is the network layout - keep it stable.
    [Networked] int RemainingTicks { get; set; }
    [Networked] NetworkBool Started { get; set; }
    [Networked] public NetworkBool Finished { get; set; }
    [Networked] int CountdownTicks { get; set; } // ticks left before the match clock starts
    public static MatchTimerState Instance { get; private set; }

    // Raised once per peer when the match ends. The result is already saved locally.
    public static event Action<MatchResult> MatchFinished;

    public bool IsPlaying => Started && !Finished;
    public bool InCountdown => Started && CountdownTicks > 0;
    public float CountdownSecondsRemaining =>
       Runner == null ? 0f : Mathf.Max(0f, CountdownTicks * Runner.DeltaTime);

    // Cars may only drive once the match object exists and its countdown is over.
    // PlayerMovement ANDs this with PlayerMatchState.CanMove.
    public static bool AllowsDriving =>
        Instance != null && Instance.Started && Instance.CountdownTicks <= 0;

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

        // New match: the previous result is history. (Restored-after-migration timers keep theirs.)
        if (!Started) MatchResultsStore.Clear();

        // Spawned also runs for objects restored after a host migration - only start once.
        if (Object.HasStateAuthority && !Started)
        {
            RemainingTicks = Mathf.RoundToInt(durationSeconds * Runner.TickRate);
            CountdownTicks = Mathf.RoundToInt(countdownSeconds * Runner.TickRate);
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

        // Pre-match countdown: cars are held still, the 90 s clock and scoring haven't started.
        if (CountdownTicks > 0)
        {
            CountdownTicks--;
            return;
        }

        var players = PlayerRoster.Active;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player == null || !player.HasState) continue;

            if (crown.HolderId != default && player.Object.Id == crown.HolderId)
            {
                // Flat rate: Score is whole points earned from total held time. CrownTicks carries
                // over between holds, so partial progress toward the next point is never lost.
                player.CrownTicks++;
                player.Score = (int)(player.CrownTicks * (double)pointsPerSecond / Runner.TickRate);

                // Longest single possession (tie-break and superlative).
                player.CurrentHoldTicks++;
                if (player.CurrentHoldTicks > player.LongestHoldTicks)
                    player.LongestHoldTicks = player.CurrentHoldTicks;
            }
            else
            {
                player.CurrentHoldTicks = 0;
            }

            TrackDistance(player);
        }

        RemainingTicks--;
        if (RemainingTicks <= 0)
        {
            RemainingTicks = 0;
            Finish();
        }
    }

    // Distance driven, flat, only while the clock runs. Positions are host-local bookkeeping; after a
    // host migration the new host simply starts measuring from where each car is.
    readonly System.Collections.Generic.Dictionary<NetworkId, Vector3> lastPositions =
        new System.Collections.Generic.Dictionary<NetworkId, Vector3>();

    void TrackDistance(PlayerMatchState player)
    {
        Vector3 now = player.transform.position;
        if (lastPositions.TryGetValue(player.Object.Id, out Vector3 before))
        {
            float dx = now.x - before.x, dz = now.z - before.z;
            float step = Mathf.Sqrt(dx * dx + dz * dz);
            if (step < 5f) player.DistanceDriven += step; // ignore teleports (respawn, migration)
        }
        lastPositions[player.Object.Id] = now;
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