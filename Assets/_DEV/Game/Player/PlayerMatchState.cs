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
public class PlayerMatchState : NetworkBehaviour
{
    [Networked] public NetworkBool IsSearching { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool CanMove { get; set; }

    // Shown above the car by PlayerNameTag. Sent up by the owning client right after spawn
    // (RPC_SetDisplayName) - the host spawns every car and has no idea what the player behind
    // a client is called until told.
    [Networked] public NetworkString<_32> DisplayName { get; set; }

    // The tick this player's own search began (set by the host when the searching RPC lands).
    // Networked so every peer can compute every searcher's elapsed time - the shared timer is
    // the highest of them, and each peer derives it locally from this.
    [Networked] int SearchStartTick { get; set; }

    public float SearchElapsedSeconds
    {
        get
        {
            if (!IsSearching || Runner == null) return 0f;
            int currentTick = Runner.Tick; // Tick converts implicitly to int
            return Mathf.Clamp((currentTick - SearchStartTick) * Runner.DeltaTime, 0f, MatchmakingConfig.SearchDurationSeconds);
        }
    }

    // Every currently-spawned instance, on every peer. Single source of truth
    // MatchmakingSessionState reads from to decide when a match should start.
    public static readonly List<PlayerMatchState> Active = new List<PlayerMatchState>();
    public static event Action RosterChanged;

    // This client's own instance. HasInputAuthority, not HasStateAuthority - under Host/Client
    // the host holds State Authority over every car, so HasStateAuthority would make Local
    // point at whichever car happened to spawn last on the host, and at nothing at all on a
    // client. Input Authority is the per-player one, assigned by the host at Runner.Spawn time.
    public static PlayerMatchState Local;

    public override void Spawned()
    {
        Active.Add(this);

        if (Object.HasInputAuthority)
        {
            Local = this;
            RPC_SetDisplayName(PlayerIdentity.LocalDisplayName);
        }

        RosterChanged?.Invoke();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
        if (Local == this) Local = null;
        RosterChanged?.Invoke();
    }

    // A client can only request its own flag change - the write itself always happens on the
    // State Authority (the host), then replicates back out to everyone automatically.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetSearching(NetworkBool searching)
    {
        if (searching && !IsSearching) SearchStartTick = Runner.Tick;
        IsSearching = searching;
        if (!searching) IsReady = false;
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
}
