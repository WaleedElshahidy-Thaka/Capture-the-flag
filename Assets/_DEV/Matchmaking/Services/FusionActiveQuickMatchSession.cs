using System;
using Fusion;

// IActiveQuickMatchSession backed by a live Photon Fusion NetworkRunner. Everything
// MatchmakingFlowController asks for is derived here from either the intrinsic Fusion
// session (PlayerCount) or the two networked types (PlayerMatchState for per-player
// searching/ready/timer, MatchmakingSessionState for the shared start decision) - the flow
// controller itself never references Fusion types directly.
//
// Read every frame by the UI, so the "which car is mine" lookup is cached on roster change
// rather than searched per property access.
public class FusionActiveQuickMatchSession : IActiveQuickMatchSession
{
    readonly NetworkRunner runner;
    PlayerMatchState ownState;

    public FusionActiveQuickMatchSession(NetworkRunner runner)
    {
        this.runner = runner;
        PlayerMatchState.RosterChanged += OnRosterChanged;
        OnRosterChanged();
    }

    bool SessionValid => runner != null && runner.SessionInfo.IsValid;
    MatchmakingSessionState SessionState => MatchmakingSessionState.Local;

    // HasInputAuthority - the per-player authority. Under Host/Client the host holds State
    // Authority over every car, so HasStateAuthority would not identify "mine" on any peer.
    void OnRosterChanged()
    {
        ownState = null;
        var players = PlayerMatchState.Active;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Object.HasInputAuthority) { ownState = players[i]; break; }
        }
        Changed?.Invoke();
    }

    public int PlayerCount => SessionValid ? runner.SessionInfo.PlayerCount : 0;
    public int MaxPlayers => SessionValid ? runner.SessionInfo.MaxPlayers : MatchmakingConfig.MaxPlayers;
    // There genuinely is a host (AutoHostOrClient), it's just never surfaced to players -
    // nothing in the UI reads this. Kept because match-authority code needs to ask.
    public bool IsHost => runner != null && runner.IsServer;

    public bool IsLocalPlayerSpawned => ownState != null;

    public bool IsSearching => ownState != null && ownState.IsSearching;
    public void SetSearching(bool searching, float alreadyElapsedSeconds = 0f) => ownState?.RPC_SetSearching(searching, alreadyElapsedSeconds);

    public float LobbyJoinSecondsRemaining => ownState != null ? ownState.LobbyJoinSecondsRemaining : 0f;
    public bool InLobby => ownState != null && ownState.InLobby;

    public int LobbyCount
    {
        get
        {
            int count = 0;
            var players = PlayerMatchState.Active;
            for (int i = 0; i < players.Count; i++)
                if (players[i].InLobby) count++;
            return count;
        }
    }

    public float ElapsedSeconds => InLobby
        ? MatchmakingSessionState.SharedElapsedSeconds()
        : ownState != null ? ownState.SearchElapsedSeconds : 0f;
    public bool BotOptionUnlocked => IsSearching && ElapsedSeconds >= MatchmakingConfig.BotOptionUnlockSeconds;

    public bool IsReady => ownState != null && ownState.IsReady;
    public void SetReady(bool ready) => ownState?.RPC_SetReady(ready);

    public void RequestStartWithBots() => ownState?.RPC_RequestStartWithBots();

    public bool MatchStarting => SessionState != null && SessionState.MatchStarting;
    public int BotCount => SessionState != null ? SessionState.BotCount : 0;

    public bool IsFrozen => SessionState != null && SessionState.Frozen;
    public float ResumeSecondsRemaining => SessionState != null ? SessionState.ResumeSecondsRemaining : 0f;

    public event Action Changed;

    public void Leave()
    {
        Detach();
        if (runner != null && runner.IsRunning) _ = runner.Shutdown();
    }

    // The runner is going away without us asking (host migration): stop listening, don't shut
    // anything down - the service is already doing that.
    public void Detach()
    {
        PlayerMatchState.RosterChanged -= OnRosterChanged;
        ownState = null;
    }
}
