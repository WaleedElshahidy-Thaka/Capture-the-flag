using Fusion;

// One singleton per Quick Match session, spawned by the host the moment the session is created.
// Holds the shared start decision (MatchStarting, BotCount) and makes it, host-side, from the
// replicated per-player state on PlayerMatchState:
//
//   - six searching                                   -> start, no bots
//   - 2+ searching, shared timer >= 30s, all ready    -> start, bots fill the empty seats
//   - one searching                                   -> starts only via RPC_RequestStartWithBots
//
// The timer itself is per-player (PlayerMatchState.SearchStartTick); "shared" just means the
// highest of the searching players' timers, which every peer derives locally.
//
// Runs at tick rate on the host, so it counts with plain loops rather than LINQ - no per-tick
// allocations.
public class MatchmakingSessionState : NetworkBehaviour
{
    [Networked] public NetworkBool MatchStarting { get; set; }
    [Networked] public int BotCount { get; set; }

    // Set on every peer this object replicates to (Spawned fires for the host and every
    // client). Read by FusionActiveQuickMatchSession, PlayerLobbyVisibility and the
    // bot-request RPC rather than searched for in the scene.
    public static MatchmakingSessionState Local;

    public override void Spawned()
    {
        Local = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this) Local = null;
    }

    // Highest timer among everyone searching - the shared timer. Computed the same way on
    // every peer from replicated state.
    public static float SharedElapsedSeconds()
    {
        float highest = 0f;
        var players = PlayerMatchState.Active;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].IsSearching) continue;
            float elapsed = players[i].SearchElapsedSeconds;
            if (elapsed > highest) highest = elapsed;
        }
        return highest;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || MatchStarting) return;

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
        // a partner who joins later isn't matched against a stale "ready".
        if (present < 2)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i].IsSearching) players[i].IsReady = false;
            return;
        }

        if (allReady && SharedElapsedSeconds() >= MatchmakingConfig.BotOptionUnlockSeconds)
            TriggerStart(MatchmakingConfig.MaxPlayers - present);
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
    }
}
