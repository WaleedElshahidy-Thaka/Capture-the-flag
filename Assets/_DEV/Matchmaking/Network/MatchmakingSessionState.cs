using System.Linq;
using Fusion;

// One singleton per Quick Match session, spawned by the host the moment the session is
// created (not when a second player joins) so the search timer starts ticking from the very
// first searcher's press. Holds the three pieces of state that must be identical for every
// player: the network-synchronized search timer, whether the match is starting, and the
// resolved bot count.
public class MatchmakingSessionState : NetworkBehaviour
{
    [Networked] public TickTimer SearchTimer { get; set; }
    [Networked] public NetworkBool MatchStarting { get; set; }
    [Networked] public int BotCount { get; set; }

    // Set on every peer this object replicates to (Spawned fires for the host and every
    // client). FusionActiveQuickMatchSession and PlayerLobbyState's bot-request RPC both
    // read this rather than searching the scene for it.
    public static MatchmakingSessionState Local;

    // Single source of truth for "how long has this session been searching" - both this
    // class's own win-condition check and FusionActiveQuickMatchSession's display/unlock
    // properties read through here, so the 30s-unlock vs 120s-display-cap split only exists
    // in one place.
    public float ElapsedSeconds => MatchmakingConfig.SearchDurationSeconds - (SearchTimer.RemainingTime(Runner) ?? 0f);
    public bool BotOptionUnlocked => ElapsedSeconds >= MatchmakingConfig.BotOptionUnlockSeconds;

    public override void Spawned()
    {
        Local = this;

        if (Object.HasStateAuthority)
            SearchTimer = TickTimer.CreateFromSeconds(Runner, MatchmakingConfig.SearchDurationSeconds);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || MatchStarting) return;

        int present = Runner.SessionInfo.PlayerCount;

        // Full room starts immediately regardless of the timer - no press, no vote, no bots.
        if (present >= MatchmakingConfig.MaxPlayers)
        {
            TriggerStart(0);
            return;
        }

        // Unanimous-ready among whoever is actually present, only once the bot option has
        // unlocked (30s, not the full 120s display cap). A lone player never satisfies
        // present > 1 - they go through PlayerLobbyState.RPC_RequestStartWithBots instead.
        if (present > 1 && BotOptionUnlocked && PlayerLobbyState.Active.Count == present && PlayerLobbyState.Active.All(p => p.IsReady))
            TriggerStart(MatchmakingConfig.MaxPlayers - present);
    }

    // Only ever called where HasStateAuthority is true: from FixedUpdateNetwork above (already
    // guarded), or from PlayerLobbyState.RPC_RequestStartWithBots (an RpcTargets.StateAuthority
    // RPC, which Fusion itself guarantees only runs on the state authority peer).
    public void TriggerStart(int botCount)
    {
        BotCount = botCount;
        MatchStarting = true;
    }
}
