using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// In-memory room backend — no networking, single process. Used so the entire lobby UI
// (create, name-collision error, visible/invisible + "make it visible", join by code +
// wrong-code error, browse list) is clickable and testable before Photon Fusion is
// configured with a real App ID. Swap for FusionRoomService in Services.cs when ready.
//
// Because everything lives in one process, "joining" a room you created yourself simulates
// a second seat filling — useful for exercising player-count/Start-button UI states, but not
// a stand-in for real concurrent multiplayer testing.
public class LocalRoomService : IRoomServiceBackend
{
    class RoomRecord
    {
        public string RoomName;
        public string RoomCode;
        public RoomVisibility Visibility;
        public int MaxPlayers;
        public int PlayerCount;
        public bool Closed;
    }

    readonly List<RoomRecord> rooms = new List<RoomRecord>();

    public IEnumerator CreateRoom(CreateRoomRequest request, Action<CreateRoomResult> onResult)
    {
        yield return null;

        string trimmedName = (request.RoomName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            onResult?.Invoke(CreateRoomResult.Fail("Room name can't be empty."));
            yield break;
        }

        bool nameTaken = rooms.Any(r => !r.Closed && string.Equals(r.RoomName, trimmedName, StringComparison.OrdinalIgnoreCase));
        if (nameTaken)
        {
            onResult?.Invoke(CreateRoomResult.Fail("A room with this name already exists."));
            yield break;
        }

        var record = new RoomRecord
        {
            RoomName = trimmedName,
            RoomCode = GenerateUniqueCode(),
            Visibility = request.Visibility,
            MaxPlayers = request.MaxPlayers,
            PlayerCount = 1
        };
        rooms.Add(record);

        onResult?.Invoke(CreateRoomResult.Ok(new LocalActiveRoomSession(record, isHost: true)));
    }

    public IEnumerator JoinByCode(string roomCode, Action<JoinRoomResult> onResult)
    {
        yield return null;

        string code = (roomCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            onResult?.Invoke(JoinRoomResult.Fail("Enter a room code."));
            yield break;
        }

        var record = rooms.FirstOrDefault(r => !r.Closed && r.RoomCode == code);
        if (record == null)
        {
            onResult?.Invoke(JoinRoomResult.Fail("Wrong room code."));
            yield break;
        }

        if (record.PlayerCount >= record.MaxPlayers)
        {
            onResult?.Invoke(JoinRoomResult.Fail("Room is full."));
            yield break;
        }

        record.PlayerCount++;
        onResult?.Invoke(JoinRoomResult.Ok(new LocalActiveRoomSession(record, isHost: false)));
    }

    public IEnumerator GetAvailableRooms(Action<RoomListResult> onResult)
    {
        yield return null;

        var summaries = rooms
            .Where(r => !r.Closed && r.Visibility == RoomVisibility.Public && r.PlayerCount < r.MaxPlayers)
            .Select(r => new RoomSummary
            {
                RoomName = r.RoomName,
                RoomCode = r.RoomCode,
                PlayerCount = r.PlayerCount,
                MaxPlayers = r.MaxPlayers
            })
            .ToList();

        onResult?.Invoke(RoomListResult.Ok(summaries));
    }

    string GenerateUniqueCode()
    {
        string code;
        int guard = 0;
        do
        {
            code = RoomCodeGenerator.Generate();
            guard++;
        }
        while (rooms.Any(r => !r.Closed && r.RoomCode == code) && guard < 10);
        return code;
    }

    class LocalActiveRoomSession : IActiveRoomSession
    {
        readonly RoomRecord record;
        readonly bool isHost;

        public LocalActiveRoomSession(RoomRecord record, bool isHost)
        {
            this.record = record;
            this.isHost = isHost;
        }

        public string RoomName => record.RoomName;
        public string RoomCode => record.RoomCode;
        public bool IsVisible => record.Visibility == RoomVisibility.Public;
        public int PlayerCount => record.PlayerCount;
        public int MaxPlayers => record.MaxPlayers;
        public bool IsHost => isHost;

        public event Action Changed;

        public IEnumerator MakeVisible(Action<MakeVisibleResult> onResult)
        {
            yield return null;

            if (!isHost)
            {
                onResult?.Invoke(MakeVisibleResult.Fail("Only the host can change room visibility."));
                yield break;
            }

            record.Visibility = RoomVisibility.Public;
            Changed?.Invoke();
            onResult?.Invoke(MakeVisibleResult.Ok());
        }

        public void Leave()
        {
            if (isHost)
                record.Closed = true;
            else
                record.PlayerCount = Math.Max(0, record.PlayerCount - 1);

            Changed?.Invoke();
        }
    }
}
