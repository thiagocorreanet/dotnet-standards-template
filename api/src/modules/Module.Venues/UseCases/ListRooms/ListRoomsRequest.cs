using Microsoft.AspNetCore.Mvc;

namespace Module.Venues.UseCases.ListRooms;

public sealed record ListRoomsRequest([FromRoute(Name = "id")] Guid VenueId, [FromQuery] bool? IsActive);
