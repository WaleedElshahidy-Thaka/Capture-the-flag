using System.Linq;
using Fusion;

// One singleton per Quick Match session, spawned the moment the session is created. Holds what
// must be identical for every searching player: the shared search timer, whether the match is
// starting, and the resolved bot count.
//
// The search timer is session-wide again, not per-player (an earlier pass tried per-player -
// each player's own countdown starting from their own Find Match click - but that meant two
// players in the same lobby saw different numbers, which is the wrong feel for a shared search).
// It doesn't start at session creation either (the complaint that predated the per-player
// attempt): it starts the first time ANY player begins searching, tracked by SearchStarted so it
// only ever starts once, then stays synced for everyone searching from then on.
public class MatchmakingSessionState : NetworkBehaviour
{
    [Networked] public NetworkBool MatchStarting { get; set; }
    [Networked] public int BotCount { get; set; }
    [Networked] NetworkBool SearchStarted { get; set; }
    [Networked] TickTimer SearchTimer { get; set; }

    public float ElapsedSeconds => MatchmakingConfig.SearchDurationSeconds - (SearchTimer.RemainingTime(Runner) ?? MatchmakingConfig.SearchDurationSeconds);
    public bool BotOptionUnlocked => SearchStarted && ElapsedSeconds >= MatchmakingConfig.BotOptionUnlockSeconds;

    // Set on every peer this object replicates to (Spawned fires for the host and every
    // client). FusionActiveQuickMatchSession and PlayerMatchState's bot-request RPC both read
    // this rather than searching the scene for it.
    public static MatchmakingSessionState Local;

    public override void Spawned()
    {
        Local = this;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || MatchStarting) return;

        // Only players who've actually clicked Find Match count here - PlayerMatchState.Active
        // also includes anyone just connected to the hub without searching, who shouldn't be
        // swept into a match they didn't ask for.
        var searching = PlayerMatchState.Active.Where(p => p.IsSearching).ToList();
        int present = searching.Count;

        if (present > 0 && SearchStarted == false)
        {
            SearchStarted = true;
            SearchTimer = TickTimer.CreateFromSeconds(Runner, MatchmakingConfig.SearchDurationSeconds);
        }

        // Full room starts immediately regardless of the timer - no press, no vote, no bots.
        if (present >= MatchmakingConfig.MaxPlayers)
        {
            TriggerStart(0);
            return;
        }

        // Unanimous-ready among whoever is actually searching, only once the shared bot option
        // has unlocked (30s since the first searcher, not the full 120s display cap). A lone
        // player never satisfies present > 1 - they go through
        // PlayerMatchState.RPC_RequestStartWithBots instead.
        if (present > 1 && BotOptionUnlocked && searching.All(p => p.IsReady))
            TriggerStart(MatchmakingConfig.MaxPlayers - present);
    }

    // Only ever called where HasStateAuthority is true: from FixedUpdateNetwork above (already
    // guarded), or from PlayerMatchState.RPC_RequestStartWithBots (an RpcTargets.StateAuthority
    // RPC, which Fusion itself guarantees only runs on the state authority peer). Under
    // Host/Client that peer is always the host.
    //
    // Releasing the movement gate happens here, host-side, for every car at once - not on each
    // client for its own car. A client has Input Authority over its car but not State Authority,
    // so it cannot write CanMove at all; the write has to originate here and replicate out.
    public void TriggerStart(int botCount)
    {
        BotCount = botCount;
        MatchStarting = true;

        foreach (var player in PlayerMatchState.Active)
            player.CanMove = true;
    }
}
