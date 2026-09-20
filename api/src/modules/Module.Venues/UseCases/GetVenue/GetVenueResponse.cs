using Module.Venues.Domain;

namespace Module.Venues.UseCases.GetVenue;

public sealed record GetVenueResponse(
    Guid Id,
    string VenueName,
    string? VenueDescription,
    string? AddressStreet,
    string? AddressNumber,
    string? AddressNeighborhood,
    string AddressCity,
    string AddressState,
    string? AddressPostalCode,
    int VenueTotalCapacity,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<GetVenueRoomResponse> Rooms);

public sealed record GetVenueRoomResponse(Guid Id, string RoomName, int RoomCapacity, RoomType RoomType, string? RoomResources, bool IsActive);
