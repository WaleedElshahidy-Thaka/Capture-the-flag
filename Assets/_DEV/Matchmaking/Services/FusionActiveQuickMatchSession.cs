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

    // HasStateAuthority, not HasInputAuthority - see PlayerMatchState.Spawned() for why: this
    // is always this client's own object regardless of whether PlayerMovement has assigned
    // Input Authority yet.
    PlayerMatchState OwnPlayerState => PlayerMatchState.Active.FirstOrDefault(p => p.Object.HasStateAuthority);
    MatchmakingSessionState SessionState => MatchmakingSessionState.Local;

    public int PlayerCount => SessionValid ? runner.SessionInfo.PlayerCount : 0;
    public int MaxPlayers => SessionValid ? runner.SessionInfo.MaxPlayers : MatchmakingConfig.MaxPlayers;
    // No host in Shared Mode - closest analog is the room's master client, kept under the same
    // name since callers just use this to show "you're in charge of X" state, not to gate any
    // Fusion authority check (those go through Object.HasStateAuthority instead).
    public bool IsHost => runner != null && runner.IsSharedModeMasterClient;

    public float ElapsedSeconds => OwnPlayerState != null ? OwnPlayerState.ElapsedSeconds : 0f;
    public bool BotOptionUnlocked => OwnPlayerState != null && OwnPlayerState.BotOptionUnlocked;

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
