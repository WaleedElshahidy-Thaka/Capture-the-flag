using System;
using System.Linq;
using Fusion;

// IActiveQuickMatchSession backed by a live Photon Fusion NetworkRunner. Everything
// MatchmakingFlowController asks for is derived here from either the intrinsic Fusion
// session (PlayerCount) or the two networked types (PlayerMatchState for this client's own
// ready/searching flags, MatchmakingSessionState for the shared start decision) - the
// flow controller itself never references Fusion types directly.
public class FusionActiveQuickMatchSession : IActiveQuickMatchSession
{
    readonly NetworkRunner runner;

    public FusionActiveQuickMatchSession(NetworkRunner runner)
    {
        this.runner = runner;
        PlayerMatchState.RosterChanged += RaiseChanged;
    }

    bool SessionValid => runner != null && runner.SessionInfo.IsValid;

    // HasInputAuthority - the per-player authority. Under Host/Client the host holds State
    // Authority over every car, so HasStateAuthority would not identify "mine" on any peer.
    PlayerMatchState OwnPlayerState => PlayerMatchState.Active.FirstOrDefault(p => p.Object.HasInputAuthority);
    MatchmakingSessionState SessionState => MatchmakingSessionState.Local;

    public int PlayerCount => SessionValid ? runner.SessionInfo.PlayerCount : 0;
    public int MaxPlayers => SessionValid ? runner.SessionInfo.MaxPlayers : MatchmakingConfig.MaxPlayers;
    // There genuinely is a host now (AutoHostOrClient), it's just never surfaced to players -
    // nothing in the UI reads this. Kept because match-authority code needs to ask.
    public bool IsHost => runner != null && runner.IsServer;

    // Session-wide, not per-player - a shared lobby clock, same number for everyone searching.
    public float ElapsedSeconds => SessionState != null ? SessionState.ElapsedSeconds : 0f;
    public bool BotOptionUnlocked => SessionState != null && SessionState.BotOptionUnlocked;

    public bool IsReady => OwnPlayerState != null && OwnPlayerState.IsReady;

    public void SetReady(bool ready)
    {
        OwnPlayerState?.RPC_SetReady(ready);
    }

    public int SearchingCount => PlayerMatchState.Active.Count(p => p.IsSearching);
    public bool IsSearching => OwnPlayerState != null && OwnPlayerState.IsSearching;

    public void SetSearching(bool searching)
    {
        OwnPlayerState?.RPC_SetSearching(searching);
    }

    public void RequestStartWithBots()
    {
        OwnPlayerState?.RPC_RequestStartWithBots();
    }

    public bool MatchStarting => SessionState != null && SessionState.MatchStarting;
    public int BotCount => SessionState != null ? SessionState.BotCount : 0;

    public event Action Changed;
    void RaiseChanged() => Changed?.Invoke();

    public void Leave()
    {
        PlayerMatchState.RosterChanged -= RaiseChanged;
        if (runner != null && runner.IsRunning) _ = runner.Shutdown();
    }
}
