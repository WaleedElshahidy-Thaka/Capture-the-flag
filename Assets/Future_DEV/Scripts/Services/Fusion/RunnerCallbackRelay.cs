using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;

// A single reusable INetworkRunnerCallbacks implementation: every method is a no-op except
// the handful this project actually cares about, each exposed as an optional Action so a
// caller can opt into just what it needs (OnSessionListUpdated for browsing, OnPlayerJoined/
// OnPlayerLeft/OnShutdown for room-state changes). Fusion requires every member of the
// interface to be implemented even if unused, so this exists once instead of being
// re-boilerplated per call site.
//
// Member names/signatures verified against the installed Fusion.Runtime.dll (2.1.2) for this
// project — if the pinned SDK version differs, recheck against it (reference doc N9).
public class RunnerCallbackRelay : INetworkRunnerCallbacks
{
    public Action<NetworkRunner, List<SessionInfo>> OnSessionListUpdatedAction;
    public Action<NetworkRunner, PlayerRef> OnPlayerJoinedAction;
    public Action<NetworkRunner, PlayerRef> OnPlayerLeftAction;
    public Action<NetworkRunner, ShutdownReason> OnShutdownAction;
    public Action<NetworkRunner, NetConnectFailedReason> OnConnectFailedAction;
    public Action<NetworkRunner, NetworkInput> OnInputAction;
    public Action<NetworkRunner> OnSceneLoadDoneAction;

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) => OnSessionListUpdatedAction?.Invoke(runner, sessionList);
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) => OnPlayerJoinedAction?.Invoke(runner, player);
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) => OnPlayerLeftAction?.Invoke(runner, player);
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) => OnShutdownAction?.Invoke(runner, shutdownReason);
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) => OnConnectFailedAction?.Invoke(runner, reason);
    public void OnInput(NetworkRunner runner, NetworkInput input) => OnInputAction?.Invoke(runner, input);

    // Unused by this project — required by the interface, intentionally empty.
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) => OnSceneLoadDoneAction?.Invoke(runner);
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
