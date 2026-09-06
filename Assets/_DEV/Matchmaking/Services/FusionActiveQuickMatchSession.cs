using System;
using System.Linq;
using Fusion;

// IActiveQuickMatchSession backed by a live Photon Fusion NetworkRunner. Everything
// MatchmakingFlowController asks for is derived here from either the intrinsic Fusion
// session (PlayerCount) or the two networked types (PlayerLobbyState for this client's own
// ready flag, MatchmakingSessionState for the shared search timer / start decision) - the
// flow controller itself never references Fusion types directly.
public class FusionActiveQuickMatchSession : IActiveQuickMatchSession
{
    readonly NetworkRunner runner;

    public FusionActiveQuickMatchSession(NetworkRunner runner)
    {
        this.runner = runner;
        PlayerLobbyState.RosterChanged += RaiseChanged;
    }

    bool SessionValid => runner != null && runner.SessionInfo.IsValid;

    PlayerLobbyState OwnPlayerState => PlayerLobbyState.Active.FirstOrDefault(p => p.Object.HasInputAuthority);
    MatchmakingSessionState SessionState => MatchmakingSessionState.Local;

    public int PlayerCount => SessionValid ? runner.SessionInfo.PlayerCount : 0;
    public int MaxPlayers => SessionValid ? runner.SessionInfo.MaxPlayers : MatchmakingConfig.MaxPlayers;
    public bool IsHost => runner != null && runner.IsServer;

    public float ElapsedSeconds => SessionState != null ? SessionState.ElapsedSeconds : 0f;
    public bool BotOptionUnlocked => SessionState != null && SessionState.BotOptionUnlocked;

    public bool IsReady => OwnPlayerState != null && OwnPlayerState.IsReady;

    public void SetReady(bool ready)
    {
        OwnPlayerState?.RPC_SetReady(ready);
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
        PlayerLobbyState.RosterChanged -= RaiseChanged;
        if (runner != null && runner.IsRunning) _ = runner.Shutdown();
    }
}
