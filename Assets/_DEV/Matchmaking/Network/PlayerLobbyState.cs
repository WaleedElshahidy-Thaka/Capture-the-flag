using System;
using System.Collections.Generic;
using Fusion;

// One instance per joined player, spawned by the host (State Authority) as soon as they join
// and despawned when they leave. Deliberately carries NO renderer and NO NetworkTransform -
// this object is pure networked identity + ready state. Where each player is drawn on screen
// is a separate, purely local decision (see Presentation/LobbySeatAssigner.cs) - if this
// object's position were synced, Fusion's own replication would fight the requirement that
// every client positions everyone (including itself) differently.
public class PlayerLobbyState : NetworkBehaviour
{
    [Networked] public NetworkBool IsReady { get; set; }

    // Every currently-spawned instance, on every peer (Spawned/Despawned fire wherever the
    // object replicates to, including the host). This is the single source of truth
    // LobbySeatAssigner and MatchmakingSessionState both read from.
    public static readonly List<PlayerLobbyState> Active = new List<PlayerLobbyState>();
    public static event Action RosterChanged;

    public override void Spawned()
    {
        Active.Add(this);
        RosterChanged?.Invoke();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
        RosterChanged?.Invoke();
    }

    // A client can only request its own ready flag change - the write itself always happens
    // on the State Authority (the host), then replicates back out to everyone automatically.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool ready)
    {
        IsReady = ready;
    }

    // Solo-only path. A lone searcher's own client already IS the host under
    // GameMode.AutoHostOrClient, so this always executes locally in practice - it goes
    // through an RPC anyway so the call path from MatchmakingFlowController is uniform
    // regardless of backend/host-ness.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestStartWithBots()
    {
        if (Runner.SessionInfo.PlayerCount != 1) return; // solo-only guard, mirrors the design intent
        MatchmakingSessionState.Local?.TriggerStart(MatchmakingConfig.MaxPlayers - Runner.SessionInfo.PlayerCount);
    }
}
