// Which lobby seats (start anchors) are taken. A seat is a PlayerMatchState.Slot; every car - a
// player's or a bot's - holds exactly one for the whole match. Host-side helper.
public static class LobbySeats
{
    public static bool IsTaken(int slot)
    {
        var cars = PlayerRoster.Active;
        for (int i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            if (car != null && car.HasState && car.Slot == slot) return true;
        }
        return false;
    }

    // The preferred seat if free, otherwise the next free one going round. -1 if every seat is taken.
    public static int FirstFree(int preferred)
    {
        int count = LobbyLayout.SlotCount;
        for (int step = 0; step < count; step++)
        {
            int slot = (preferred + step) % count;
            if (!IsTaken(slot)) return slot;
        }
        return -1;
    }
}
