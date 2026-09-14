using System;
using System.Collections;
using System.Collections.Generic;
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

    readonly PlayerLobbySpawner playerLobbySpawner = new PlayerLobbySpawner();

    NetworkRunner connectingRunner;

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

        var runnerGO = new GameObject($"NetworkRunner-QuickMatch-{RoomCodeGenerator.Generate()}"); // debug label only, never displayed, never passed as SessionName
        var runner = runnerGO.AddComponent<NetworkRunner>();
        runner.ProvideInput = true; // required for OnInputAction below to ever fire
        connectingRunner = runner;

        runner.AddCallbacks(new RunnerCallbackRelay
        {
            OnPlayerJoinedAction = playerLobbySpawner.HandlePlayerJoined,
            OnPlayerLeftAction = playerLobbySpawner.HandlePlayerLeft,
            OnInputAction = (r, input) => input.Set(PlayerInputSampler.Sample())
        });

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        var args = new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            PlayerCount = MatchmakingConfig.MaxPlayers,
            IsVisible = true,
            IsOpen = true,
            Scene = sceneInfo,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                [GameIdPropertyKey] = gameId
            }
        };

        var startTask = runner.StartGame(args);
        while (!startTask.IsCompleted) yield return null;

        connectingRunner = null;

        if (!startTask.Result.Ok)
        {
            UnityEngine.Object.Destroy(runnerGO);
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

        onResult?.Invoke(StartQuickMatchResult.Ok(new FusionActiveQuickMatchSession(runner)));
    }

    public void CancelQuickMatch()
    {
        if (connectingRunner == null) return;

        var runner = connectingRunner;
        connectingRunner = null;
        if (runner.IsRunning) _ = runner.Shutdown();
        else UnityEngine.Object.Destroy(runner.gameObject);
    }
}
