// Composition root — the only place a new service instance is created.
// To add a new service: create YourService.cs, then add one line below.
public static class Services
{
    public static readonly IGameService Game = new GameService();
}
