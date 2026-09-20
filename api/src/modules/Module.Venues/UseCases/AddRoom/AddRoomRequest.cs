using System.Text.Json.Serialization;
using Module.Venues.Domain;

namespace Module.Venues.UseCases.AddRoom;

public sealed record AddRoomRequest(string RoomName, int RoomCapacity, RoomType RoomType, string? RoomResources)
{
    [JsonIgnore]
    public Guid VenueId { get; init; }
}
