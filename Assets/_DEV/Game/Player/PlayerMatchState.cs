using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

// Ready/searching/movement-gate state for one player's PlayerCar - a sibling component of
// PlayerMovement, not merged into it, so "am I allowed to be matched/move" stays separate from
// "how do I actually drive" (PlayerMovement.FixedUpdateNetwork only asks this component's
// CanMove, it owns none of this state itself).
//
// Replaces the old separate PlayerLobbyState object: previously the lobby placeholder (ready/
// searching data, spawned at connect) and the real car (spawned only once a match started) were
// two different NetworkObjects that had to be kept in sync. Now there's one real PlayerCar,
// present and positioned from the moment you connect, just not drivable (CanMove) until a match
// starts - so the same object that used to be lobby-only data now lives right on the car itself.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerMatchState : NetworkBehaviour
{
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool IsSearching { get; set; }
    [Networked] public NetworkBool CanMove { get; set; }

    // The search timer/elapsed-time display lives on MatchmakingSessionState, not here - it's a
    // shared lobby clock (synced for everyone searching together), not a per-player one. See
    // that class's doc comment for why a per-player timer was tried and reverted.

    // Every currently-spawned instance, on every peer. Single source of truth
    // MatchmakingSessionState reads from to decide when a match should start.
    public static readonly List<PlayerMatchState> Active = new List<PlayerMatchState>();
    public static event Action RosterChanged;

    // This client's own instance - set in Spawned() the same way MatchmakingSessionState.Local
    // is, so MatchStarter can flip CanMove on exactly the right object without a scene search.
    public static PlayerMatchState Local;

    // HasInputAuthority, not HasStateAuthority - under Host/Client the host holds State Authority
    // over every car, so HasStateAuthority would make Local point at whichever car happened to
    // spawn last on the host, and at nothing at all on a client. Input Authority is the
    // per-player one, assigned by the host at Runner.Spawn time, so it is already correct by the
    // time this runs on any peer.
    public override void Spawned()
    {
        Active.Add(this);
        if (Object.HasInputAuthority) Local = this;
        RosterChanged?.Invoke();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
        if (Local == this) Local = null;
        RosterChanged?.Invoke();
    }

    // A client can only request its own ready/searching flag change - the write itself always
    // happens on the State Authority (the host), then replicates back out to everyone
    // automatically.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool ready)
    {
        IsReady = ready;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetSearching(NetworkBool searching)
    {
        IsSearching = searching;
    }

    // Solo-only path. A lone searcher already has State Authority over their own
    // PlayerMatchState under Shared Mode, so this always executes locally in practice - it goes
    // through an RPC anyway so the call path from MatchmakingFlowController is uniform
    // regardless of backend/authority. Guards on how many players are actually searching, not
    // total connected players - other players can be connected to the hub without searching,
    // and shouldn't block (or count toward) a lone searcher's solo start.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestStartWithBots()
    {
        int searchingCount = Active.Count(p => p.IsSearching);
        if (searchingCount != 1) return;
        MatchmakingSessionState.Local?.TriggerStart(MatchmakingConfig.MaxPlayers - searchingCount);
    }
}
