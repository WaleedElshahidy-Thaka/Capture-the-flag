using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// Real Photon Fusion 2 Quick Match backend. GameMode.Shared, not Host/Client - no player's own
// device is silently the arbiter, matching the same "everyone just finds a match and plays" call
// made for Drive.unity (see Documentation/Networking_Progress.md). No room name, no room code as
// SessionName, no visibility choice - a shared SessionName with no explicit room code is Fusion's
// own "quick join" behaviour, so the first searcher transparently opens the session and later
// searchers are placed into it.
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
            GameMode = GameMode.Shared,
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

        // Shared Mode has no server - "IsSharedModeMasterClient" is the closest analog (the
        // room's first/designated peer), used here purely to make sure exactly one copy of the
        // singleton session state gets spawned rather than one per peer.
        if (runner.IsSharedModeMasterClient)
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
