using Fusion;
using UnityEngine;

// One singleton per Quick Match session, spawned by the host the moment the session is created.
// Holds the shared start decision (MatchStarting, BotCount) and makes it, host-side, from the
// replicated per-player state on PlayerMatchState:
//
//   - six searching                                   -> start, no bots
//   - 2+ searching, shared timer >= 30s, all ready    -> start, bots fill the empty seats
//   - one searching                                   -> starts only via RPC_RequestStartWithBots
//
// The timer itself is per-player (PlayerMatchState.SearchStartTick); "shared" just means the
// highest timer among the players in the lobby, which every peer derives locally. Two searchers
// don't join the lobby the instant they both exist: the host stamps them Found, both count the
// same three seconds down ("Player found! Joining lobby in N"), and only then do they see each
// other and share the timer.
//
// It also owns the freeze after a host migration: the new host restores this object from the
// old host's snapshot, holds every car still until the players are back (or the reconnect
// window runs out), then counts everyone down to the same resume tick.
//
// Runs at tick rate on the host, so it counts with plain loops rather than LINQ - no per-tick
// allocations.
public class MatchmakingSessionState : NetworkBehaviour, IAfterHostMigration
{
    [Networked] public NetworkBool MatchStarting { get; set; }
    [Networked] public int BotCount { get; set; }

    // Migration state. Migrating is set by the new host at hand-over and cleared once the
    // reconnect window has been dealt with; MigratedAtTick is the new runner's tick at that
    // moment; Frozen holds the cars while players reconnect; ResumeAtTick is the tick everyone
    // unfreezes on (0 = not decided yet, still waiting for players).
    [Networked] NetworkBool Migrating { get; set; }
    [Networked] int MigratedAtTick { get; set; }
    [Networked] public NetworkBool Frozen { get; set; }
    [Networked] int ResumeAtTick { get; set; }

    // Seconds until play resumes; negative while the new host is still waiting for players to
    // come back, so the UI can say "waiting" rather than show a number it can't promise.
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

    // Set on every peer this object replicates to (Spawned fires for the host and every
    // client). Read by FusionActiveQuickMatchSession, PlayerLobbyVisibility and the
    // bot-request RPC rather than searched for in the scene.
    public static MatchmakingSessionState Local;

    // Host only, once every object from the old host's snapshot is back. PlayerLobbySpawner
    // uses it to settle players who reconnected before the cars they own existed again.
    public static event System.Action<NetworkRunner> HostMigrationRestored;

    public override void Spawned()
    {
        Local = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this) Local = null;
    }

    // Highest timer among everyone in the lobby - the shared timer. Computed the same way on
    // every peer from replicated state. A searcher still counting down isn't in it yet.
    public static float SharedElapsedSeconds()
    {
        float highest = 0f;
        var players = PlayerMatchState.Active;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].InLobby) continue;
            float elapsed = players[i].SearchElapsedSeconds;
            if (elapsed > highest) highest = elapsed;
        }
        return highest;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (Migrating) TickMigration();
        if (Frozen || MatchStarting) return;

        TickLobby();
    }

    void TickLobby()
    {
        var players = PlayerMatchState.Active;
        int present = 0;
        bool allReady = true;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!player.IsSearching) continue;
            present++;
            if (!player.IsReady) allReady = false;
        }

        // Full room starts immediately - no vote, no bots.
        if (present >= MatchmakingConfig.MaxPlayers)
        {
            TriggerStart(0);
            return;
        }

        // A lone searcher's ready flag means nothing (they start via the solo RPC); clear it so
        // a partner who joins later isn't matched against a stale "ready". A countdown they were
        // in the middle of is abandoned too - the partner it announced has gone.
        if (present < 2)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].IsSearching) continue;
                players[i].IsReady = false;
                players[i].AbortFoundCountdown();
            }
            return;
        }

        // Two or more searching: anyone not yet found starts their lobby-join countdown now.
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsSearching) players[i].MarkFound(Runner.Tick);

        if (allReady && SharedElapsedSeconds() >= MatchmakingConfig.BotOptionUnlockSeconds)
            TriggerStart(MatchmakingConfig.MaxPlayers - present);
    }

    // After a hand-over, cars sit ownerless until their players reconnect (PlayerLobbySpawner
    // hands each one back by OwnerToken). Mid-match: hold everything still, then count down to
    // one shared resume tick once everyone is back or the window closes; whoever didn't make
    // it is a bot from then on. In the lobby: nothing to freeze, but a car whose player never
    // returns is removed once the window closes so it doesn't sit there counting as a searcher.
    void TickMigration()
    {
        int now = Runner.Tick;
        bool windowClosed = (now - MigratedAtTick) * Runner.DeltaTime >= MatchmakingConfig.ReconnectWindowSeconds;
        var players = PlayerMatchState.Active;

        if (!MatchStarting)
        {
            if (!windowClosed) return;
            for (int i = players.Count - 1; i >= 0; i--)
                if (players[i].IsBot) Runner.Despawn(players[i].Object);
            Migrating = false;
            return;
        }

        if (!Frozen) return;

        if (ResumeAtTick == 0)
        {
            bool everyoneBack = true;
            for (int i = 0; i < players.Count; i++)
                if (players[i].IsBot && players[i].OwnerToken.Length > 0) { everyoneBack = false; break; }

            if (everyoneBack || windowClosed)
                ResumeAtTick = now + Mathf.CeilToInt(MatchmakingConfig.ResumeCountdownSeconds * Runner.TickRate);
            return;
        }

        if (now < ResumeAtTick) return;

        // Resume. Anyone still missing is a bot for the rest of the round, and the session shuts
        // its door again - reconnects were the only reason it was open.
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsBot) players[i].HandToBot();

        Frozen = false;
        ResumeAtTick = 0;
        Migrating = false;
        CloseSession();
    }

    // Only ever called where HasStateAuthority is true: from FixedUpdateNetwork above (already
    // guarded), or from PlayerMatchState.RPC_RequestStartWithBots (an RpcTargets.StateAuthority
    // RPC, which Fusion itself guarantees only runs on the state authority peer). Under
    // Host/Client that peer is always the host.
    //
    // Releasing the movement gate happens here, host-side, for every searching car at once - a
    // client has Input Authority over its car but not State Authority, so it cannot write
    // CanMove itself. Cars that weren't searching stay parked.
    public void TriggerStart(int botCount)
    {
        BotCount = botCount;
        MatchStarting = true;

        var players = PlayerMatchState.Active;
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsSearching) players[i].CanMove = true;

        CloseSession();
    }

    // A match in progress admits nobody: invisible to matchmaking, and closed to joins. Before
    // this existed a player opening the game mid-round was dropped straight into it - visible to
    // everyone, unable to drive, no UI. Late join and rejoin are not supported by design; the
    // one exception is reconnecting after a host migration, which reopens the door briefly.
    void CloseSession()
    {
        Runner.SessionInfo.IsVisible = false;
        Runner.SessionInfo.IsOpen = false;
    }

    // New host, state just restored from the old host's snapshot. The old host is the one
    // player who is certainly not coming back; their car goes to the bot driver now rather
    // than after the reconnect window.
    public void AfterHostMigration()
    {
        if (!Object.HasStateAuthority) return;

        Migrating = true;
        MigratedAtTick = Runner.Tick;
        ResumeAtTick = 0;
        Frozen = MatchStarting;

        var players = PlayerMatchState.Active;
        for (int i = 0; i < players.Count; i++)
            if (players[i].WasHost) players[i].HandToBot();

        // Reconnecting players join by token, not by matchmaking - stay invisible, stay open
        // until the resume closes it (or, in the lobby, reopen to matchmaking).
        Runner.SessionInfo.IsVisible = !MatchStarting;
        Runner.SessionInfo.IsOpen = true;

        HostMigrationRestored?.Invoke(Runner);
    }
}
