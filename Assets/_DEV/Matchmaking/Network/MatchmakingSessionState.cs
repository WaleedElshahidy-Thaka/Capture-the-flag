using System;
using Fusion;
using UnityEngine;

// One per Quick Match session, spawned by the host. The host uses it to decide when the match
// starts, from the replicated per-player state:
//   6 searching                                  -> start, no bots
//   2+ searching, shared timer >= 30 s, all ready -> start, bots fill the empty seats
//   1 searching                                  -> only via PlayerMatchState.RPC_RequestStartWithBots
// It also freezes the cars after a host migration until players are back, then resumes everyone
// on the same tick.
public class MatchmakingSessionState : NetworkBehaviour, IAfterHostMigration
{
    // Declaration order is the network layout - keep it stable.
    [Networked] public NetworkBool MatchStarting { get; set; }
    [Networked] public int BotCount { get; set; }
    [Networked] NetworkBool Migrating { get; set; }       // set by the new host until reconnects are settled
    [Networked] int MigratedAtTick { get; set; }
    [Networked] public NetworkBool Frozen { get; set; }   // cars held still while players reconnect
    [Networked] int ResumeAtTick { get; set; }            // 0 = still waiting for players

    // The instance on this peer (host and clients alike).
    public static MatchmakingSessionState Local;

    // Host only: raised once every object from the old host's snapshot exists again.
    public static event Action<NetworkRunner> HostMigrationRestored;

    // Seconds until play resumes; -1 while still waiting for players (the UI shows no number).
    public float ResumeSecondsRemaining
    {
        get
        {
            if (!Frozen) return 0f;
            if (ResumeAtTick == 0) return -1f;
            int now = Runner.Tick;
            return Mathf.Max(0f, (ResumeAtTick - now) * Runner.DeltaTime);
        }
    }

    public override void Spawned()
    {
        Local = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this) Local = null;
    }

    // Host only (no allocations - runs every tick).
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (Migrating) TickMigration();
        if (Frozen || MatchStarting) return;

        TickLobby();
    }

    // Host only. Releases every searching car and closes the session to newcomers.
    public void TriggerStart(int botCount)
    {
        BotCount = botCount;
        MatchStarting = true;

        var players = PlayerRoster.Active;
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsSearching) players[i].CanMove = true;

        CloseSession();
    }

    // ─── Lobby ──────────────────────────────────────────────────────────

    void TickLobby()
    {
        int searching = PlayerRoster.CountSearching();

        if (searching >= MatchmakingConfig.MaxPlayers)
        {
            TriggerStart(0);
            return;
        }

        if (searching < 2)
        {
            ResetLoneSearcher();
            return;
        }

        MarkSearchersFound();

        bool timerUnlocked = PlayerRoster.SharedElapsedSeconds() >= MatchmakingConfig.BotOptionUnlockSeconds;
        if (PlayerRoster.AllSearchingReady() && timerUnlocked)
            TriggerStart(MatchmakingConfig.MaxPlayers - searching);
    }

    // A lone searcher's ready flag is meaningless and any countdown's partner has left.
    void ResetLoneSearcher()
    {
        var players = PlayerRoster.Active;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].IsSearching) continue;
            players[i].IsReady = false;
            players[i].AbortFoundCountdown();
        }
    }

    void MarkSearchersFound()
    {
        var players = PlayerRoster.Active;
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsSearching) players[i].MarkFound(Runner.Tick);
    }

    // No late joins: a running match is hidden from matchmaking and closed.
    void CloseSession()
    {
        Runner.SessionInfo.IsVisible = false;
        Runner.SessionInfo.IsOpen = false;
    }

    // ─── Host migration ─────────────────────────────────────────────────

    // New host, right after restoring from the old host's snapshot.
    public void AfterHostMigration()
    {
        if (!Object.HasStateAuthority) return;

        Migrating = true;
        MigratedAtTick = Runner.Tick;
        ResumeAtTick = 0;
        Frozen = MatchStarting;

        // The old host is the one player certainly not coming back.
        var players = PlayerRoster.Active;
        for (int i = 0; i < players.Count; i++)
            if (players[i].WasHost) players[i].HandToBot();

        // Reconnects come by token, so the session stays invisible mid-match but open.
        Runner.SessionInfo.IsVisible = !MatchStarting;
        Runner.SessionInfo.IsOpen = true;

        HostMigrationRestored?.Invoke(Runner);
    }

    void TickMigration()
    {
        int now = Runner.Tick;
        bool windowClosed = (now - MigratedAtTick) * Runner.DeltaTime >= MatchmakingConfig.ReconnectWindowSeconds;

        if (!MatchStarting)
        {
            if (windowClosed) FinishLobbyMigration();
            return;
        }

        if (!Frozen) return;

        if (ResumeAtTick == 0)
        {
            if (!PlayerRoster.AnyOwnerMissing() || windowClosed)
                ResumeAtTick = now + Mathf.CeilToInt(MatchmakingConfig.ResumeCountdownSeconds * Runner.TickRate);
            return;
        }

        if (now >= ResumeAtTick) ResumeMatch();
    }

    // In the lobby: cars whose players never came back are removed.
    void FinishLobbyMigration()
    {
        var players = PlayerRoster.Active;
        for (int i = players.Count - 1; i >= 0; i--) // reverse: Despawn removes from the list
            if (players[i].IsBot) Runner.Despawn(players[i].Object);
        Migrating = false;
    }

    // Mid-match: anyone still missing is a bot for the rest of the round.
    void ResumeMatch()
    {
        var players = PlayerRoster.Active;
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsBot) players[i].HandToBot();

        Frozen = false;
        ResumeAtTick = 0;
        Migrating = false;
        CloseSession();
    }
}
