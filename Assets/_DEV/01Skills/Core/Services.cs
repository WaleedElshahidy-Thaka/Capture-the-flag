// Composition root — the only place a new service instance is created.
// To add a new service: create YourService.cs, then add one line below.
public static class Services
{
    public static readonly IGameService Game = new GameService();

    // Room/lobby backend. LocalRoomService is in-memory and needs no setup, so the whole
    // lobby UI is testable today; swap in FusionRoomService once a Photon Fusion App ID is
    // configured (Tools > Fusion > Fusion Hub) to switch to real networking — no screen
    // code changes either way, since screens only ever depend on the interfaces below.
    static readonly IRoomServiceBackend room = new LocalRoomService();
    public static readonly IRoomCreator RoomCreator = room;
    public static readonly IRoomJoiner RoomJoiner = room;
    public static readonly IRoomBrowser RoomBrowser = room;
}
