using System.Text.Json.Serialization;
using Module.Venues.Domain;

namespace Module.Venues.UseCases.UpdateRoom;

public sealed record UpdateRoomRequest(string RoomName, int RoomCapacity, RoomType RoomType, string? RoomResources, bool IsActive)
{
    [JsonIgnore]
    public Guid VenueId { get; init; }

    [JsonIgnore]
    public Guid RoomId { get; init; }
}
