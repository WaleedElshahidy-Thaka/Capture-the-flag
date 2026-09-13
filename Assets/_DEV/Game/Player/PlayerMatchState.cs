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

    // Per-player, not a single session-wide timer - starts counting from when THIS player
    // clicked Find Match, not from whenever the session itself first spawned. Two players who
    // click Find Match at different times see different elapsed times, which is the point.
    [Networked] TickTimer SearchTimer { get; set; }

    public float ElapsedSeconds => MatchmakingConfig.SearchDurationSeconds - (SearchTimer.RemainingTime(Runner) ?? 0f);
    public bool BotOptionUnlocked => IsSearching && ElapsedSeconds >= MatchmakingConfig.BotOptionUnlockSeconds;

    // Every currently-spawned instance, on every peer. Single source of truth
    // MatchmakingSessionState reads from to decide when a match should start.
    public static readonly List<PlayerMatchState> Active = new List<PlayerMatchState>();
    public static event Action RosterChanged;

    // This client's own instance - set in Spawned() the same way MatchmakingSessionState.Local
    // is, so MatchStarter can flip CanMove on exactly the right object without a scene search.
    public static PlayerMatchState Local;

    // HasStateAuthority, not HasInputAuthority - Input Authority is assigned by
    // PlayerMovement.Spawned() on the same NetworkObject, and Spawned() call order between
    // sibling NetworkBehaviours isn't something to depend on. HasStateAuthority is true
    // immediately, for whichever peer spawned this (see PlayerLobbySpawner) - always this
    // client's own object, in Shared Mode, exactly like PlayerMovement's own check.
    public override void Spawned()
    {
        Active.Add(this);
        if (Object.HasStateAuthority) Local = this;
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
        if (searching) SearchTimer = TickTimer.CreateFromSeconds(Runner, MatchmakingConfig.SearchDurationSeconds);
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
