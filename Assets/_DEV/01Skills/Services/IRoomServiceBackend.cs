// Aggregate of the three room-service interfaces so Services.cs can construct one backend
// instance (LocalRoomService or FusionRoomService) and expose it through three separate,
// narrow fields. Screens only ever depend on the individual interfaces, never on this one.
public interface IRoomServiceBackend : IRoomCreator, IRoomJoiner, IRoomBrowser
{
}
