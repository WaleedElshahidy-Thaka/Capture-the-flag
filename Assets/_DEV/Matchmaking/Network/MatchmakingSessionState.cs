using System.Linq;
using Fusion;

// One singleton per Quick Match session, spawned the moment the session is created. Holds only
// what must be identical for every player: whether the match is starting, and the resolved bot
// count. The search timer itself is per-player now (see PlayerMatchState) - a session-wide
// timer doesn't make sense once players can join a session that's already open without
// searching, and start searching (or not) whenever they click Find Match.
public class MatchmakingSessionState : NetworkBehaviour
{
    [Networked] public NetworkBool MatchStarting { get; set; }
    [Networked] public int BotCount { get; set; }

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

        // Full room starts immediately regardless of the timer - no press, no vote, no bots.
        if (present >= MatchmakingConfig.MaxPlayers)
        {
            TriggerStart(0);
            return;
        }

        // Unanimous-ready among whoever is actually searching, only once every searching
        // player's own bot option has unlocked (30s each, not the full 120s display cap). A
        // lone player never satisfies present > 1 - they go through
        // PlayerMatchState.RPC_RequestStartWithBots instead.
        if (present > 1 && searching.All(p => p.IsReady && p.BotOptionUnlocked))
            TriggerStart(MatchmakingConfig.MaxPlayers - present);
    }

    // Only ever called where HasStateAuthority is true: from FixedUpdateNetwork above (already
    // guarded), or from PlayerMatchState.RPC_RequestStartWithBots (an RpcTargets.StateAuthority
    // RPC, which Fusion itself guarantees only runs on the state authority peer).
    public void TriggerStart(int botCount)
    {
        BotCount = botCount;
        MatchStarting = true;
    }
}
