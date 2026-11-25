using System;
using System.Collections;

public interface IRoomJoiner
{
    IEnumerator JoinByCode(string roomCode, Action<JoinRoomResult> onResult);
}
