namespace Module.Venues.UseCases.UpdateVenue;

public sealed record UpdateVenueResponse(Guid Id, string VenueName, DateTimeOffset? UpdatedAt);
