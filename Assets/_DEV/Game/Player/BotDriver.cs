// Input source for a car with no player behind it (host only). A bot is an input source, not a
// second movement system: its input goes through PlayerMovement.SimulateTick like a player's.
// Placeholder: the car parks. The real bot AI replaces the body of Think and nothing else.
public static class BotDriver
{
    public static PlayerNetInput Think(PlayerMovement car) => default;
}
