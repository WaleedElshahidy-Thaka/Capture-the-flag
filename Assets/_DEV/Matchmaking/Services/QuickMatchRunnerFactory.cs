using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds NetworkRunners and StartGameArgs for Quick Match. Used for both the first connect and
// every host-migration reconnect, so the two can't drift apart.
public class QuickMatchRunnerFactory
{
    public const string GameIdPropertyKey = "GameId";

    readonly PlayerLobbySpawner spawner;
    readonly Action<NetworkRunner, HostMigrationToken> onHostMigration;

    public QuickMatchRunnerFactory(PlayerLobbySpawner spawner, Action<NetworkRunner, HostMigrationToken> onHostMigration)
    {
        this.spawner = spawner;
        this.onHostMigration = onHostMigration;
    }

    public NetworkRunner CreateRunner()
    {
        var runnerGO = new GameObject("NetworkRunner-QuickMatch");
        var runner = runnerGO.AddComponent<NetworkRunner>();
        runner.ProvideInput = true; // required for OnInput to fire

        // Fusion steps PhysX once per network tick (instead of Unity's own FixedUpdate), so the
        // drive model's velocity writes and the physics step can't drift apart.
        var physics = runnerGO.AddComponent<RunnerSimulatePhysics>();
        physics.Update3DPhysicsScene = true;
        physics.Update2DPhysicsScene = false;

        runner.AddCallbacks(new RunnerCallbackRelay
        {
            OnPlayerJoinedAction = spawner.HandlePlayerJoined,
            OnPlayerLeftAction = spawner.HandlePlayerLeft,
            OnInputAction = (r, input) => input.Set(PlayerInputSampler.Sample()),
            OnHostMigrationAction = onHostMigration,
        });

        return runner;
    }

    // Common to every StartGame. The connection token identifies the player across host migrations.
    public StartGameArgs BaseArgs(string gameId)
    {
        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        return new StartGameArgs
        {
            PlayerCount = MatchmakingConfig.MaxPlayers,
            Scene = sceneInfo,
            ConnectionToken = PlayerIdentity.ConnectionToken,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                [GameIdPropertyKey] = gameId
            }
        };
    }

    // New host only: re-spawn every networked object from the old host's snapshot, in place and
    // with all [Networked] state copied. Nobody owns anything yet; ownership is handed back as
    // players reconnect (PlayerLobbySpawner).
    public static void RestoreSnapshot(NetworkRunner runner)
    {
        foreach (var resumeObject in runner.GetResumeSnapshotNetworkObjects())
        {
            Vector3? position = null;
            Quaternion? rotation = null;
            if (resumeObject.TryGetBehaviour<NetworkTRSP>(out var trsp))
            {
                position = trsp.Data.Position;
                rotation = trsp.Data.Rotation;
            }

            runner.Spawn(resumeObject, position, rotation, inputAuthority: null,
                onBeforeSpawned: (r, newObject) => newObject.CopyStateFrom(resumeObject));
        }
    }
}
