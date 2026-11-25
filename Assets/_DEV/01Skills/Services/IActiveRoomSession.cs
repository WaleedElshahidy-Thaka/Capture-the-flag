using System;
using System.Collections;

// Represents "the room I'm currently in" — returned by IRoomCreator/IRoomJoiner on success.
// Backed by either LocalRoomService (in-memory) or FusionRoomService (real Photon Fusion
// session); the Waiting Room screen never knows which.
public interface IActiveRoomSession
{
    string RoomName { get; }
    string RoomCode { get; }
    bool IsVisible { get; }
    int PlayerCount { get; }
    int MaxPlayers { get; }
    bool IsHost { get; }

    // Raised whenever any of the above may have changed (a player joined/left, visibility flipped).
    event Action Changed;

    // Host-only. No-op (via Fail) if called by a non-host.
    IEnumerator MakeVisible(Action<MakeVisibleResult> onResult);

    void Leave();
}
