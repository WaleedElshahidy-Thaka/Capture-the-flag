using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// Real Photon Fusion 2 Quick Match backend. Modeled on Future_DEV/Scripts/Services/
// FusionRoomService.cs's coroutine shape (spin up a NetworkRunner, AddCallbacks, build
// NetworkSceneInfo, StartGame, poll Task.IsCompleted) but simplified: no room name, no room
// code as SessionName, no visibility choice - GameMode.AutoHostOrClient with no SessionName
// is Fusion's own "quick join" behaviour (confirmed from Photon's shipped
// FusionMenuConnectionBehaviourSdk.cs sample), so the first searcher transparently becomes
// host and later searchers are placed into that same session.
//
// OPEN VERIFICATION ITEM: whether AutoHostOrClient actually filters candidate sessions by
// SessionProperties[GameIdPropertyKey] or only tags the resulting session for bookkeeping is
// not confirmed - test with two clients passing different gameId values before relying on
// this to keep two different games' matchmaking pools apart.
//
// Not wired in yet - MatchmakingServices.cs defaults to QuickMatchLocalService. Swap the
// `quickMatch` field there to go live, same pattern as the shelved FusionRoomService.
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
        connectingRunner = runner;

        runner.AddCallbacks(new RunnerCallbackRelay
        {
            OnPlayerJoinedAction = playerLobbySpawner.HandlePlayerJoined,
            OnPlayerLeftAction = playerLobbySpawner.HandlePlayerLeft
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
