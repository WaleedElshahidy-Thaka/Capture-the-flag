using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Sockets;

// Step 3: Shared Mode - no host/client, matching "everyone just finds a match and plays
// together" with no visible authority (step 1 was Single to prove the networked PlayerMovement
// conversion worked at all; step 2 was Host/Client to prove real cross-process replication
// worked). Every peer spawns and drives their own car in response to their OWN join - Runner.Spawn
// is what makes that peer State Authority for it, which is also what PlayerMovement.Spawned()
// uses to recognize "this is my own car" and claim Input Authority + camera target.
//
// Implements INetworkRunnerCallbacks directly rather than NetworkEvents - NetworkEvents doesn't
// expose OnPlayerJoined (only OnInput, OnShutdown, and a handful of others), so the full
// interface is needed here regardless, which also folds OnInput into the same class instead of
// two separate hookups.
[RequireComponent(typeof(NetworkRunner))]
public class DriveNetworkBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    const string SessionName = "DriveTest";

    [SerializeField] NetworkObject playerPrefab;

    NetworkRunner runner;

    async void Start()
    {
        runner = GetComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = SessionName,
            SceneManager = sceneManager,
            Scene = SceneRef.FromIndex(gameObject.scene.buildIndex),
        });

        if (result.Ok) Debug.Log("[DriveNetworkBootstrap] Joined shared session.");
        else Debug.LogError($"[DriveNetworkBootstrap] StartGame failed: {result.ShutdownReason} {result.ErrorMessage}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // This fires on every peer for every player that joins (including remote ones) - only
        // ever spawn in response to MY OWN join, or every peer would end up spawning a car for
        // every other peer too.
        if (player != runner.LocalPlayer) return;

        Vector3 spawnPosition = new Vector3(player.PlayerId * 4f, 1f, 0f);
        runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        var data = new PlayerNetInput();

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) data.ThrottleAxis += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) data.ThrottleAxis -= 1f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) data.SteerAxis -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) data.SteerAxis += 1f;

        data.Boost = keyboard.leftShiftKey.isPressed;
        data.Drift = keyboard.leftCtrlKey.isPressed;

        input.Set(data);
    }

    // Unused callbacks - required by the interface, no-ops for this project.
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
}
