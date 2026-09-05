using System;
using System.Collections.Generic;

// ─── Requests ──────────────────────────────────────────────────────────────

public enum RoomVisibility
{
    Private,
    Public
}

public class CreateRoomRequest
{
    public readonly string RoomName;
    public readonly int MaxPlayers;
    public readonly RoomVisibility Visibility;

    public CreateRoomRequest(string roomName, int maxPlayers, RoomVisibility visibility)
    {
        RoomName = roomName;
        MaxPlayers = maxPlayers;
        Visibility = visibility;
    }
}

// ─── Data ──────────────────────────────────────────────────────────────────

// A room as shown in the "search for available seat" list — only public/visible
// rooms are ever surfaced this way. Carries RoomCode so joining from the list can
// reuse the same JoinByCode path, even though the code itself isn't displayed here.
[Serializable]
public class RoomSummary
{
    public string RoomName;
    public string RoomCode;
    public int PlayerCount;
    public int MaxPlayers;
}

// ─── Immutable result types (mirrors GameModels.cs's Ok/Fail pattern) ──────

public class CreateRoomResult
{
    public bool Success { get; private set; }
    public IActiveRoomSession Room { get; private set; }
    public string Error { get; private set; }

    public static CreateRoomResult Ok(IActiveRoomSession room) => new CreateRoomResult { Success = true, Room = room };
    public static CreateRoomResult Fail(string error) => new CreateRoomResult { Success = false, Error = error };
}

public class JoinRoomResult
{
    public bool Success { get; private set; }
    public IActiveRoomSession Room { get; private set; }
    public string Error { get; private set; }

    public static JoinRoomResult Ok(IActiveRoomSession room) => new JoinRoomResult { Success = true, Room = room };
    public static JoinRoomResult Fail(string error) => new JoinRoomResult { Success = false, Error = error };
}

public class RoomListResult
{
    public bool Success { get; private set; }
    public List<RoomSummary> Rooms { get; private set; }
    public string Error { get; private set; }

    public static RoomListResult Ok(List<RoomSummary> rooms) => new RoomListResult { Success = true, Rooms = rooms };
    public static RoomListResult Fail(string error) => new RoomListResult { Success = false, Rooms = new List<RoomSummary>(), Error = error };
}

public class MakeVisibleResult
{
    public bool Success { get; private set; }
    public string Error { get; private set; }

    public static MakeVisibleResult Ok() => new MakeVisibleResult { Success = true };
    public static MakeVisibleResult Fail(string error) => new MakeVisibleResult { Success = false, Error = error };
}
