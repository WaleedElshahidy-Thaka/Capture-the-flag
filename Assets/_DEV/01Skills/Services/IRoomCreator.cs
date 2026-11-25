using System;
using System.Collections;

public interface IRoomCreator
{
    IEnumerator CreateRoom(CreateRoomRequest request, Action<CreateRoomResult> onResult);
}
