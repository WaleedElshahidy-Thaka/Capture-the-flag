using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// Real Photon Fusion 2 Quick Match backend. GameMode.AutoHostOrClient: the first searcher
// transparently becomes host and later searchers join as clients, with no room name, room code
// or visibility choice anywhere in the flow. Players never see or choose this - from their side
// it's still "press Find Match, get put in with people", which is the actual requirement. The
// host/client split is an implementation detail they are never shown.
//
// Deliberately NOT GameMode.Shared, which an earlier pass used. Shared Mode has no single
// authority to resolve a chassis-to-chassis contact, and this game is built around contact -
// two peers each resolving the same bump against their own stale proxy of the other car
// disagree about who won it, which decides who holds the flag. Collision, Contact & Recovery
// (GDD doc 05) requires one authority per contact; Host/Client is what provides it. It is also
// what makes the drive model's rollback/determinism contract (GDD doc 03) achievable at all.
//
// Host migration: with NetworkProjectConfig > HostMigration enabled, the host pushes a state
// snapshot to the cloud every few seconds. When the host drops, Photon elects one of the
// remaining clients and hands every survivor a HostMigrationToken via OnHostMigration. Each
// peer shuts its dead runner down and starts a fresh one with the token - the elected peer as
// the new host, restoring every networked object from the snapshot (HostMigrationResume); the
// rest as clients reconnecting to it. Who owns which car afterwards is settled by connection
// token (PlayerIdentity.InstallId), see PlayerLobbySpawner; when play resumes is settled by
// MatchmakingSessionState.
//
// OPEN VERIFICATION ITEM: whether joining without a SessionName actually filters candidate
// sessions by SessionProperties[GameIdPropertyKey] or only tags the resulting session for
// bookkeeping is not confirmed - test with two clients passing different gameId values before
// relying on this to keep two different games' matchmaking pools apart.
//
// This is the live backend - MatchmakingServices.cs points `quickMatch` here. Swap it back to
// `new QuickMatchLocalService()` for solo/no-network UI iteration.
public class QuickMatchFusionService : IQuickMatchService
{
    public const string GameIdPropertyKey = "GameId";
    const int MigrationAttempts = 5;
    const int MigrationRetryMilliseconds = 1500;

    readonly PlayerLobbySpawner playerLobbySpawner = new PlayerLobbySpawner();

    NetworkRunner connectingRunner;
    FusionActiveQuickMatchSession activeSession;
    string gameId;

    public event Action HostLost;
    public event Action<IActiveQuickMatchSession> SessionRestored;
    public event Action<string> SessionFailed;

    static bool HasAppId
    {
        get
        {
            var settings = PhotonAppSettings.Global;
            return settings != null && settings.AppSettings != null && !string.IsNullOrEmpty(settings.AppSettings.AppIdFusion);
        }
    }

    public IEnumerator StartQuickMatch(string gameId, Action<StartQuickMatchResult> onResult)
    {
        if (!HasAppId)
        {
            onResult?.Invoke(StartQuickMatchResult.Fail("Photon Fusion App ID isn't configured yet."));
            yield break;
        }

        this.gameId = gameId;
        var runner = CreateRunner();
        connectingRunner = runner;

        var args = BaseArgs();
        args.GameMode = GameMode.AutoHostOrClient;
        args.IsVisible = true;
        args.IsOpen = true;

        var startTask = runner.StartGame(args);
        while (!startTask.IsCompleted) yield return null;

        connectingRunner = null;

        if (!startTask.Result.Ok)
        {
            UnityEngine.Object.Destroy(runner.gameObject);
            onResult?.Invoke(StartQuickMatchResult.Fail($"Could not find or open a match: {startTask.Result.ShutdownReason}"));
            yield break;
        }

        // Host only - under AutoHostOrClient the host is the sole spawner, so exactly one copy
        // of the singleton session state exists and every client receives it by replication.
        if (runner.IsServer)
        {
            var sessionStatePrefab = Resources.Load<NetworkObject>("MatchmakingSessionState");
            if (sessionStatePrefab == null)
                Debug.LogError("[QuickMatchFusionService] Resources/MatchmakingSessionState.prefab not found.");
            else
                runner.Spawn(sessionStatePrefab, inputAuthority: null);
        }

        activeSession = new FusionActiveQuickMatchSession(runner);
        onResult?.Invoke(StartQuickMatchResult.Ok(activeSession));
    }

    public void CancelQuickMatch()
    {
        if (connectingRunner == null) return;

        var runner = connectingRunner;
        connectingRunner = null;
        if (runner.IsRunning) _ = runner.Shutdown();
        else UnityEngine.Object.Destroy(runner.gameObject);
    }

    // One runner setup for both the first connect and every hand-over, so the two can't drift.
    NetworkRunner CreateRunner()
    {
        var runnerGO = new GameObject("NetworkRunner-QuickMatch");
        var runner = runnerGO.AddComponent<NetworkRunner>();
        runner.ProvideInput = true; // required for OnInputAction below to ever fire

        // Fusion steps PhysX itself, once per network tick, instead of Unity stepping it on its
        // own 50Hz FixedUpdate while the drive model writes velocity at Fusion's 60Hz. Without
        // this the two cadences drift against each other - some ticks' velocity writes are
        // integrated twice, some never - which shows up as rotation/position jitter and breaks
        // the "same input, same tick, same result" premise the drive model is built on (doc 03).
        // A SimulationBehaviour on the runner's own GameObject is picked up automatically.
        var physics = runnerGO.AddComponent<Fusion.Addons.Physics.RunnerSimulatePhysics>();
        physics.Update3DPhysicsScene = true;
        physics.Update2DPhysicsScene = false;

        runner.AddCallbacks(new RunnerCallbackRelay
        {
            OnPlayerJoinedAction = playerLobbySpawner.HandlePlayerJoined,
            OnPlayerLeftAction = playerLobbySpawner.HandlePlayerLeft,
            OnInputAction = (r, input) => input.Set(PlayerInputSampler.Sample()),
            OnHostMigrationAction = (r, token) => _ = HandleHostMigration(r, token),
        });

        return runner;
    }

    // Everything a StartGame has in common between the first connect and a hand-over. The
    // connection token is how the host tells players apart across sessions - it's the one
    // thing about a player that a migration doesn't reset.
    StartGameArgs BaseArgs()
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

    // Runs on every surviving peer. The token says which role this peer has in the new session.
    // async rather than a coroutine because there is no MonoBehaviour left to host one once the
    // runner is gone, and this is the shape Fusion documents for it.
    async Task HandleHostMigration(NetworkRunner deadRunner, HostMigrationToken token)
    {
        try
        {
            HostLost?.Invoke();
            activeSession?.Detach();
            activeSession = null;

            await deadRunner.Shutdown(true, ShutdownReason.HostMigration);

            // Photon may not have the new room ready the instant the token arrives, so a client's
            // first attempt can fail; a few tries over several seconds covers it.
            for (int attempt = 1; attempt <= MigrationAttempts; attempt++)
            {
                var runner = CreateRunner();
                var args = BaseArgs();
                args.HostMigrationToken = token;
                args.HostMigrationResume = HostMigrationResume;
                // Reconnects come by token, not matchmaking - invisible. Open, because those
                // reconnects have to get in; MatchmakingSessionState closes it again on resume.
                args.IsVisible = false;
                args.IsOpen = true;

                var result = await runner.StartGame(args);
                if (result.Ok)
                {
                    activeSession = new FusionActiveQuickMatchSession(runner);
                    SessionRestored?.Invoke(activeSession);
                    return;
                }

                Debug.LogWarning($"[QuickMatchFusionService] Host migration attempt {attempt}/{MigrationAttempts} failed: {result.ShutdownReason}");
                if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
                await Task.Delay(MigrationRetryMilliseconds);
            }

            SessionFailed?.Invoke("Could not rejoin the match after the host left.");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SessionFailed?.Invoke(e.Message);
        }
    }

    // New host only. Rebuild every networked object from the old host's last snapshot: same
    // prefab, same state (CopyStateFrom brings all [Networked] properties across - searching,
    // ready, CanMove, names, owner tokens, MatchStarting...). Cars get their snapshot position
    // and rotation so they reappear where they were, not at a spawn point. Nobody owns anything
    // yet - input authority is handed back per player as they reconnect (PlayerLobbySpawner) -
    // and per-object fix-ups that depend on the new runner run in IAfterHostMigration.
    static void HostMigrationResume(NetworkRunner runner)
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
