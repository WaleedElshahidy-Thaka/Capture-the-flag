using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// Real Photon Fusion 2 room backend. Written and cross-checked against the API actually
// present in this project's installed Fusion.Runtime.dll / Fusion.Realtime.dll (2.1.2) —
// type/method/enum-member names below were verified against that assembly, not guessed.
// Still: confirm against whichever SDK version ends up pinned before shipping, same as the
// reference doc's own N9 recommendation.
//
// Not wired in yet — Services.cs defaults to LocalRoomService. Swap the `room` field there
// once a Fusion App ID is set (Tools > Fusion > Fusion Hub, or PhotonAppSettings.Global
// .AppSettings.AppIdFusion directly) so the whole UI has been exercised against the local
// backend first.
public class FusionRoomService : IRoomServiceBackend
{
    const string RoomNamePropertyKey = "RoomName";
    const float BrowseTimeoutSeconds = 5f;

    // PhotonAppSettings.Global is null until the Fusion Hub setup wizard (Tools > Fusion >
    // Fusion Hub) has created the PhotonAppSettings asset at least once — guard against that
    // rather than throwing a NullReferenceException out of every call.
    static bool HasAppId
    {
        get
        {
            var settings = PhotonAppSettings.Global;
            return settings != null && settings.AppSettings != null && !string.IsNullOrEmpty(settings.AppSettings.AppIdFusion);
        }
    }

    public IEnumerator CreateRoom(CreateRoomRequest request, Action<CreateRoomResult> onResult)
    {
        if (!HasAppId)
        {
            onResult?.Invoke(CreateRoomResult.Fail("Photon Fusion App ID isn't configured yet."));
            yield break;
        }

        string trimmedName = (request.RoomName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            onResult?.Invoke(CreateRoomResult.Fail("Room name can't be empty."));
            yield break;
        }

        // Private rooms are never visible to this query (by design — see the reference doc's
        // Design Goal 2), so this only catches collisions against other PUBLIC rooms. True
        // global uniqueness would need a custom name-registry backend, which the doc
        // explicitly avoids building.
        var visibleCo = FetchVisibleSessions();
        yield return visibleCo;
        var visibleSessions = lastVisibleSessions;

        bool nameTaken = visibleSessions != null && visibleSessions.Any(s => NameOf(s) == trimmedName);
        if (nameTaken)
        {
            onResult?.Invoke(CreateRoomResult.Fail("A room with this name already exists."));
            yield break;
        }

        string code = RoomCodeGenerator.Generate();
        var runnerGO = new GameObject($"NetworkRunner-Host-{code}");
        var runner = runnerGO.AddComponent<NetworkRunner>();
        runner.AddCallbacks(new RunnerCallbackRelay());

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        var args = new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = code,
            PlayerCount = request.MaxPlayers,
            IsVisible = request.Visibility == RoomVisibility.Public,
            IsOpen = true,
            Scene = sceneInfo,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                [RoomNamePropertyKey] = trimmedName
            }
        };

        var startTask = runner.StartGame(args);
        while (!startTask.IsCompleted) yield return null;

        if (!startTask.Result.Ok)
        {
            UnityEngine.Object.Destroy(runnerGO);
            onResult?.Invoke(CreateRoomResult.Fail($"Failed to create room: {startTask.Result.ShutdownReason}"));
            yield break;
        }

        onResult?.Invoke(CreateRoomResult.Ok(new FusionActiveRoomSession(runner, trimmedName, isHost: true)));
    }

    public IEnumerator JoinByCode(string roomCode, Action<JoinRoomResult> onResult)
    {
        if (!HasAppId)
        {
            onResult?.Invoke(JoinRoomResult.Fail("Photon Fusion App ID isn't configured yet."));
            yield break;
        }

        string code = (roomCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            onResult?.Invoke(JoinRoomResult.Fail("Enter a room code."));
            yield break;
        }

        var runnerGO = new GameObject($"NetworkRunner-Client-{code}");
        var runner = runnerGO.AddComponent<NetworkRunner>();
        runner.AddCallbacks(new RunnerCallbackRelay());

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        var args = new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = code,
            Scene = sceneInfo
        };

        var joinTask = runner.StartGame(args);
        while (!joinTask.IsCompleted) yield return null;

        if (!joinTask.Result.Ok)
        {
            UnityEngine.Object.Destroy(runnerGO);
            var reason = joinTask.Result.ShutdownReason;
            string message = reason == ShutdownReason.GameNotFound ? "Wrong room code."
                : reason == ShutdownReason.GameIsFull ? "Room is full."
                : $"Could not join: {reason}";
            onResult?.Invoke(JoinRoomResult.Fail(message));
            yield break;
        }

        string roomName = code;
        if (runner.SessionInfo.IsValid && runner.SessionInfo.Properties.TryGetValue(RoomNamePropertyKey, out var prop) && prop.PropertyValue != null)
            roomName = prop.PropertyValue.ToString();

        onResult?.Invoke(JoinRoomResult.Ok(new FusionActiveRoomSession(runner, roomName, isHost: false)));
    }

    public IEnumerator GetAvailableRooms(Action<RoomListResult> onResult)
    {
        if (!HasAppId)
        {
            onResult?.Invoke(RoomListResult.Fail("Photon Fusion App ID isn't configured yet."));
            yield break;
        }

        yield return FetchVisibleSessions();
        var sessions = lastVisibleSessions;

        if (sessions == null)
        {
            onResult?.Invoke(RoomListResult.Fail("Timed out fetching the room list."));
            yield break;
        }

        var summaries = sessions
            .Where(s => s.IsOpen && s.IsVisible && s.PlayerCount < s.MaxPlayers)
            .Select(s => new RoomSummary
            {
                RoomName = NameOf(s),
                RoomCode = s.Name,
                PlayerCount = s.PlayerCount,
                MaxPlayers = s.MaxPlayers
            })
            .ToList();

        onResult?.Invoke(RoomListResult.Ok(summaries));
    }

    static string NameOf(SessionInfo session)
    {
        if (session.Properties != null && session.Properties.TryGetValue(RoomNamePropertyKey, out var prop) && prop.PropertyValue != null)
            return prop.PropertyValue.ToString();
        return session.Name;
    }

    // Spins up a throwaway runner, joins Fusion's session lobby, waits for the first
    // OnSessionListUpdated callback (or a timeout), then tears the runner down. Used by both
    // CreateRoom's name-uniqueness pre-check and GetAvailableRooms.
    List<SessionInfo> lastVisibleSessions;

    IEnumerator FetchVisibleSessions()
    {
        lastVisibleSessions = null;
        List<SessionInfo> result = null;
        bool done = false;

        var browseGO = new GameObject("NetworkRunner-Browse");
        var browseRunner = browseGO.AddComponent<NetworkRunner>();
        browseRunner.AddCallbacks(new RunnerCallbackRelay
        {
            OnSessionListUpdatedAction = (_, list) => { result = list; done = true; }
        });

        var joinTask = browseRunner.JoinSessionLobby(SessionLobby.ClientServer);
        while (!joinTask.IsCompleted) yield return null;

        if (!joinTask.Result.Ok)
        {
            UnityEngine.Object.Destroy(browseGO);
            lastVisibleSessions = new List<SessionInfo>();
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + BrowseTimeoutSeconds;
        while (!done && Time.realtimeSinceStartup < deadline)
            yield return null;

        var shutdownTask = browseRunner.Shutdown();
        while (!shutdownTask.IsCompleted) yield return null;
        UnityEngine.Object.Destroy(browseGO);

        lastVisibleSessions = result ?? new List<SessionInfo>();
    }
}
