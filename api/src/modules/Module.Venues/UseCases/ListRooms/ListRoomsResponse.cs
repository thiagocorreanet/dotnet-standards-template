using Module.Venues.Domain;

namespace Module.Venues.UseCases.ListRooms;

public sealed record ListRoomsItemResponse(Guid Id, string RoomName, int RoomCapacity, RoomType RoomType, string? RoomResources, bool IsActive);
