using System;
using System.Collections;
using Fusion;

// IActiveRoomSession backed by a live Photon Fusion NetworkRunner — one per room the local
// player is currently in, as host (Create Room) or as a joined client (Join Room).
public class FusionActiveRoomSession : IActiveRoomSession
{
    readonly NetworkRunner runner;
    readonly bool isHost;
    readonly string roomName;

    public FusionActiveRoomSession(NetworkRunner runner, string roomName, bool isHost)
    {
        this.runner = runner;
        this.roomName = roomName;
        this.isHost = isHost;

        var relay = new RunnerCallbackRelay
        {
            OnPlayerJoinedAction = (_, __) => Changed?.Invoke(),
            OnPlayerLeftAction = (_, __) => Changed?.Invoke(),
            OnShutdownAction = (_, __) => Changed?.Invoke()
        };
        runner.AddCallbacks(relay);
    }

    bool Valid => runner != null && runner.SessionInfo.IsValid;

    public string RoomName => roomName;
    public string RoomCode => Valid ? runner.SessionInfo.Name : string.Empty;
    public bool IsVisible => Valid && runner.SessionInfo.IsVisible;
    public int PlayerCount => Valid ? runner.SessionInfo.PlayerCount : 0;
    public int MaxPlayers => Valid ? runner.SessionInfo.MaxPlayers : 0;
    public bool IsHost => isHost;

    public event Action Changed;

    public IEnumerator MakeVisible(Action<MakeVisibleResult> onResult)
    {
        if (!isHost)
        {
            onResult?.Invoke(MakeVisibleResult.Fail("Only the host can change room visibility."));
            yield break;
        }

        if (!Valid)
        {
            onResult?.Invoke(MakeVisibleResult.Fail("Room is no longer active."));
            yield break;
        }

        // NOTE: relies on SessionInfo.IsVisible having a live setter that pushes the change
        // to the Photon Cloud room (this is what the reference doc's N10 calls "the Fusion
        // question underneath all of it" — confirm against the pinned SDK version).
        runner.SessionInfo.IsVisible = true;
        Changed?.Invoke();
        onResult?.Invoke(MakeVisibleResult.Ok());
    }

    public void Leave()
    {
        if (runner != null && runner.IsRunning)
            _ = runner.Shutdown();
    }
}
