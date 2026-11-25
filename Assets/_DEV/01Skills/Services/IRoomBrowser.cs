using System;
using System.Collections;

public interface IRoomBrowser
{
    IEnumerator GetAvailableRooms(Action<RoomListResult> onResult);
}
