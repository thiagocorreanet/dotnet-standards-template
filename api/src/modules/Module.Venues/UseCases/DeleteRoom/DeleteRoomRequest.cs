namespace Module.Venues.UseCases.DeleteRoom;

public sealed record DeleteRoomRequest(Guid VenueId, Guid RoomId);
