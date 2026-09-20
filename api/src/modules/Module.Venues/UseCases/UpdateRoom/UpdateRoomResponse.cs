using Module.Venues.Domain;

namespace Module.Venues.UseCases.UpdateRoom;

public sealed record UpdateRoomResponse(Guid Id, Guid VenueId, string RoomName, int RoomCapacity, RoomType RoomType, bool IsActive);
