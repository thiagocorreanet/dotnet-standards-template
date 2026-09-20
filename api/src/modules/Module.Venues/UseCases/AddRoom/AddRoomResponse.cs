using Module.Venues.Domain;

namespace Module.Venues.UseCases.AddRoom;

public sealed record AddRoomResponse(Guid Id, Guid VenueId, string RoomName, int RoomCapacity, RoomType RoomType);
