// The input source for a car nobody is driving. Glowtag FD-07: "A bot is an input source, not
// a second movement system" - whatever this returns goes through PlayerMovement.SimulateTick
// exactly like a player's keyboard does, on the host only.
//
// Placeholder: the car parks. A car ends up here when its player leaves mid-match or fails to
// come back after a host migration, and the field has to stay at six (FD-07) - so the car
// stays, keeps its name, position, and later its score and Crown, and this drives it. The real
// bot AI (Phase 6) replaces the body of Think and nothing else.
public static class BotDriver
{
    public static PlayerNetInput Think(PlayerMovement car) => default;
}
