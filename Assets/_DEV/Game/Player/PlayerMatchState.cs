using Fusion;
using UnityEngine;

// Matchmaking state of one car: searching / found / in lobby / ready / allowed to move, plus who
// owns it. Separate from PlayerMovement on purpose: "may I move" vs "how do I drive".
// Clients request changes via RPC; the host (State Authority) writes them.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerMatchState : NetworkBehaviour, IAfterHostMigration
{
    // Declaration order is the network layout - keep it stable.
    [Networked] public NetworkBool IsSearching { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool CanMove { get; set; }
    [Networked] public NetworkString<_32> DisplayName { get; set; }
    // PlayerIdentity install id of the owner - survives host migration, unlike PlayerRef. Empty = bot.
    [Networked] public NetworkString<_32> OwnerToken { get; set; }
    // The old host never comes back after a migration, so its car goes to the bot driver at once.
    [Networked] public NetworkBool WasHost { get; set; }
    [Networked] int SearchStartTick { get; set; }
    [Networked] int FoundTick { get; set; } // tick the host paired this searcher; 0 = not yet
    [Networked] public int CrownTicks { get; set; }
    [Networked] public int Score { get; set; }


    public bool IsFound => IsSearching && FoundTick != 0;
    public bool InLobby => IsFound && LobbyJoinSecondsRemaining <= 0f;
    public bool IsBot => Object.InputAuthority == PlayerRef.None;
    

    // Networked properties are only readable between Spawned and Despawned. Unity callbacks
    // (LateUpdate) can outlive the runner during a host migration, so they check this first.
    public bool HasState { get; private set; }

    // Every peer counts the same countdown from the same host-stamped tick, so all cars appear together.
    public float LobbyJoinSecondsRemaining
    {
        get
        {
            if (!IsFound || Runner == null) return 0f;
            return Mathf.Max(0f, MatchmakingConfig.LobbyJoinCountdownSeconds - ElapsedSince(FoundTick));
        }
    }

    public float SearchElapsedSeconds
    {
        get
        {
            if (!IsSearching || Runner == null) return 0f;
            return Mathf.Clamp(ElapsedSince(SearchStartTick), 0f, MatchmakingConfig.SearchDurationSeconds);
        }
    }

    bool wasMine;

    float ElapsedSince(int tick)
    {
        int now = Runner.Tick;
        return (now - tick) * Runner.DeltaTime;
    }

    public override void Spawned()
    {
        HasState = true;
        PlayerRoster.Register(this);
        RefreshOwnership();
        PlayerRoster.NotifyChanged();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        HasState = false;
        PlayerRoster.Unregister(this);
        PlayerRoster.NotifyChanged();
    }

    // Input authority changes at runtime (host migration hand-back, player leaving) and Fusion
    // has no callback for it, so it's polled: one bool compare per car per frame.
    public override void Render()
    {
        if (Object.HasInputAuthority == wasMine) return;
        RefreshOwnership();
        PlayerRoster.NotifyChanged();
    }

    void RefreshOwnership()
    {
        wasMine = Object.HasInputAuthority;
        PlayerRoster.SetOwnership(this, wasMine);

        // The host spawns every car and doesn't know our name until we send it.
        if (wasMine) RPC_SetDisplayName(PlayerIdentity.LocalDisplayName);
    }

    // ─── Client requests (run on the host) ──────────────────────────────

    // alreadyElapsedSeconds back-dates the timer so it continues from the local one started at the click.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetSearching(NetworkBool searching, float alreadyElapsedSeconds)
    {
        if (searching && !IsSearching)
            SearchStartTick = Runner.Tick - Mathf.RoundToInt(Mathf.Max(0f, alreadyElapsedSeconds) * Runner.TickRate);
        IsSearching = searching;
        if (!searching)
        {
            IsReady = false;
            FoundTick = 0;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool ready)
    {
        IsReady = ready;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetDisplayName(string name)
    {
        DisplayName = name;
    }

    // Solo only: honoured when exactly one player is searching.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestStartWithBots()
    {
        int searchingCount = PlayerRoster.CountSearching();
        if (searchingCount != 1) return;
        MatchmakingSessionState.Local?.TriggerStart(MatchmakingConfig.MaxPlayers - searchingCount);
    }

    // ─── Host-only commands ─────────────────────────────────────────────

    // Stamps once, so a later joiner doesn't restart everyone's countdown.
    public void MarkFound(int tick)
    {
        if (FoundTick == 0) FoundTick = tick;
    }

    // The partner left before the countdown finished: back to plain searching.
    public void AbortFoundCountdown()
    {
        if (IsFound && !InLobby) FoundTick = 0;
    }

    // The player is gone for good - this car is bot-driven from now on.
    public void HandToBot()
    {
        OwnerToken = "";
        WasHost = false;
        IsReady = false;
        if (Object.InputAuthority != PlayerRef.None) Object.RemoveInputAuthority();
    }

    // Ticks belong to the old runner, so tick-based timers are re-based on the new host.
    // Search timers restart; a player already found goes straight into the lobby.
    public void AfterHostMigration()
    {
        if (!Object.HasStateAuthority) return;

        int now = Runner.Tick;
        if (IsSearching) SearchStartTick = now;
        if (FoundTick != 0) FoundTick = now - Mathf.CeilToInt(MatchmakingConfig.LobbyJoinCountdownSeconds * Runner.TickRate);
    }
}
