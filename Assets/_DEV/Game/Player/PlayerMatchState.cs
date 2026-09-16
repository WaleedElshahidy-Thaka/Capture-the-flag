using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Ready/searching/movement-gate state for one player's PlayerCar - a sibling component of
// PlayerMovement, not merged into it, so "am I allowed to be matched/move" stays separate from
// "how do I actually drive" (PlayerMovement.FixedUpdateNetwork only asks this component's
// CanMove, it owns none of this state itself).
//
// One real PlayerCar per player, spawned by the host the moment that player connects (scene
// start), seated in the lobby row, not drivable (CanMove) until a match starts.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerMatchState : NetworkBehaviour, IAfterHostMigration
{
    [Networked] public NetworkBool IsSearching { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool CanMove { get; set; }

    // Shown above the car by PlayerNameTag. Sent up by the owning client right after spawn
    // (RPC_SetDisplayName) - the host spawns every car and has no idea what the player behind
    // a client is called until told.
    [Networked] public NetworkString<_32> DisplayName { get; set; }

    // Who this car belongs to, as PlayerIdentity.InstallId text - the one thing about a player
    // that survives a host migration (PlayerRefs don't). Empty means nobody: a bot-driven car.
    [Networked] public NetworkString<_32> OwnerToken { get; set; }

    // True on the host's own car. After a migration the new host uses it to find the old host's
    // car - the one player who is guaranteed not to come back - and hand it to the bot driver
    // right away instead of waiting the reconnect window for them.
    [Networked] public NetworkBool WasHost { get; set; }

    // The tick this player's own search began (set by the host when the searching RPC lands).
    // Networked so every peer can compute every searcher's elapsed time - the shared timer is
    // the highest of them, and each peer derives it locally from this.
    [Networked] int SearchStartTick { get; set; }

    // The tick the host paired this searcher with at least one other (0 = not yet). Every peer
    // counts the same LobbyJoinCountdownSeconds down from it and InLobby flips on the same tick
    // everywhere - which is what makes two cars appear to each other at the same moment rather
    // than whenever each peer happened to notice.
    [Networked] int FoundTick { get; set; }

    public bool IsFound => IsSearching && FoundTick != 0;

    public float LobbyJoinSecondsRemaining
    {
        get
        {
            if (!IsFound || Runner == null) return 0f;
            int currentTick = Runner.Tick;
            return Mathf.Max(0f, MatchmakingConfig.LobbyJoinCountdownSeconds - (currentTick - FoundTick) * Runner.DeltaTime);
        }
    }

    public bool InLobby => IsFound && LobbyJoinSecondsRemaining <= 0f;

    public float SearchElapsedSeconds
    {
        get
        {
            if (!IsSearching || Runner == null) return 0f;
            int currentTick = Runner.Tick; // Tick converts implicitly to int
            return Mathf.Clamp((currentTick - SearchStartTick) * Runner.DeltaTime, 0f, MatchmakingConfig.SearchDurationSeconds);
        }
    }

    // A car with no player behind it. The host feeds it BotDriver input.
    public bool IsBot => Object.InputAuthority == PlayerRef.None;

    // Networked properties are only readable while the object is spawned. Unity-driven
    // callbacks (LateUpdate) can run on a car whose runner has already freed its state - a host
    // migration shuts the old runner down a frame before the GameObjects go - so anything that
    // reads state from outside Fusion's own callbacks checks this first.
    public bool HasState => Object != null && Object.IsValid;

    // Every currently-spawned instance, on every peer. Single source of truth
    // MatchmakingSessionState reads from to decide when a match should start.
    public static readonly List<PlayerMatchState> Active = new List<PlayerMatchState>();
    public static event Action RosterChanged;

    // This client's own instance. HasInputAuthority, not HasStateAuthority - under Host/Client
    // the host holds State Authority over every car, so HasStateAuthority would make Local
    // point at whichever car happened to spawn last on the host, and at nothing at all on a
    // client. Input Authority is the per-player one, assigned by the host at Runner.Spawn time.
    public static PlayerMatchState Local;

    bool wasMine;

    public override void Spawned()
    {
        Active.Add(this);
        RefreshOwnership();
        RosterChanged?.Invoke();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
        if (Local == this) Local = null;
        RosterChanged?.Invoke();
    }

    // Input authority isn't fixed for life: after a host migration every car comes back
    // ownerless and the new host hands each one to its player as they reconnect, and a leaver's
    // car is handed to the bot driver. Fusion has no callback for that, so it's polled here -
    // one bool compare per car per frame.
    public override void Render()
    {
        if (Object.HasInputAuthority != wasMine)
        {
            RefreshOwnership();
            RosterChanged?.Invoke();
        }
    }

    void RefreshOwnership()
    {
        wasMine = Object.HasInputAuthority;

        if (wasMine)
        {
            Local = this;
            // Fresh spawn: the host doesn't know our name yet. After a migration the name is
            // already in the copied state, and sending it again is harmless.
            RPC_SetDisplayName(PlayerIdentity.LocalDisplayName);
        }
        else if (Local == this)
        {
            Local = null;
        }
    }

    // A client can only request its own flag change - the write itself always happens on the
    // State Authority (the host), then replicates back out to everyone automatically.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetSearching(NetworkBool searching)
    {
        if (searching && !IsSearching) SearchStartTick = Runner.Tick;
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

    // Solo-only path: guards on how many players are searching, not how many are connected -
    // others can be in the arena without searching, and neither block nor count toward a lone
    // searcher's solo start. Runs on the state authority (the host) whichever peer sent it.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestStartWithBots()
    {
        int searchingCount = 0;
        for (int i = 0; i < Active.Count; i++)
            if (Active[i].IsSearching) searchingCount++;

        if (searchingCount != 1) return;
        MatchmakingSessionState.Local?.TriggerStart(MatchmakingConfig.MaxPlayers - searchingCount);
    }

    // Host-only (called from MatchmakingSessionState's FixedUpdateNetwork, which is guarded on
    // state authority). Stamps once; a player already found or already in the lobby keeps their
    // original tick so a third joiner doesn't restart everyone's countdown.
    public void MarkFound(int tick)
    {
        if (FoundTick == 0) FoundTick = tick;
    }

    // Host-only. Aborts a countdown whose partner left before it finished - the player drops
    // back to plain searching. Someone already in the lobby stays there (alone, until the next
    // searcher joins them).
    public void AbortFoundCountdown()
    {
        if (IsFound && !InLobby) FoundTick = 0;
    }

    // Host-only. The player behind this car is gone for good - it drives itself from now on.
    public void HandToBot()
    {
        OwnerToken = "";
        WasHost = false;
        IsReady = false;
        if (Object.InputAuthority != PlayerRef.None) Object.RemoveInputAuthority();
    }

    // New host, right after the state was restored from the old host's snapshot. Tick counts
    // belong to the runner that made them, and this is a new runner - so anything measured in
    // ticks is re-based here rather than trusted. Search timers restart from zero; a player who
    // was already found goes straight to InLobby instead of counting down again.
    public void AfterHostMigration()
    {
        if (!Object.HasStateAuthority) return;

        int now = Runner.Tick;
        if (IsSearching) SearchStartTick = now;
        if (FoundTick != 0) FoundTick = now - Mathf.CeilToInt(MatchmakingConfig.LobbyJoinCountdownSeconds * Runner.TickRate);
    }
}
