using System;
using Fusion;

// IActiveQuickMatchSession backed by a live NetworkRunner. Translates the networked types
// (PlayerMatchState, MatchmakingSessionState) into the Fusion-free contract the flow controller uses.
public class FusionActiveQuickMatchSession : IActiveQuickMatchSession
{
    readonly NetworkRunner runner;
    PlayerMatchState ownState; // cached on roster change; the UI reads this every frame

    public event Action Changed;

    public FusionActiveQuickMatchSession(NetworkRunner runner)
    {
        this.runner = runner;
        PlayerRoster.Changed += OnRosterChanged;
        OnRosterChanged();
    }

    bool SessionValid => runner != null && runner.SessionInfo.IsValid;
    static MatchmakingSessionState SessionState => MatchmakingSessionState.Local;

    void OnRosterChanged()
    {
        ownState = PlayerRoster.FindWithInputAuthority();
        Changed?.Invoke();
    }

    public int PlayerCount => SessionValid ? runner.SessionInfo.PlayerCount : 0;
    public int MaxPlayers => SessionValid ? runner.SessionInfo.MaxPlayers : MatchmakingConfig.MaxPlayers;
    public bool IsHost => runner != null && runner.IsServer;

    public bool IsLocalPlayerSpawned => ownState != null;

    public bool IsSearching => ownState != null && ownState.IsSearching;
    public void SetSearching(bool searching, float alreadyElapsedSeconds = 0f) => ownState?.RPC_SetSearching(searching, alreadyElapsedSeconds);

    public float LobbyJoinSecondsRemaining => ownState != null ? ownState.LobbyJoinSecondsRemaining : 0f;
    public bool InLobby => ownState != null && ownState.InLobby;
    public int LobbyCount => PlayerRoster.CountInLobby();

    public float ElapsedSeconds => InLobby
        ? PlayerRoster.SharedElapsedSeconds()
        : ownState != null ? ownState.SearchElapsedSeconds : 0f;
    public bool BotOptionUnlocked => IsSearching && ElapsedSeconds >= MatchmakingConfig.BotOptionUnlockSeconds;

    public bool IsReady => ownState != null && ownState.IsReady;
    public void SetReady(bool ready) => ownState?.RPC_SetReady(ready);

    public void RequestStartWithBots() => ownState?.RPC_RequestStartWithBots();

    public bool MatchStarting => SessionState != null && SessionState.MatchStarting;
    public int BotCount => SessionState != null ? SessionState.BotCount : 0;

    public bool IsFrozen => SessionState != null && SessionState.Frozen;
    public float ResumeSecondsRemaining => SessionState != null ? SessionState.ResumeSecondsRemaining : 0f;

    public void Leave()
    {
        Detach();
        if (runner != null && runner.IsRunning) _ = runner.Shutdown();
    }

    // The runner is going away by itself (host migration): stop listening, don't shut it down.
    public void Detach()
    {
        PlayerRoster.Changed -= OnRosterChanged;
        ownState = null;
    }
}
