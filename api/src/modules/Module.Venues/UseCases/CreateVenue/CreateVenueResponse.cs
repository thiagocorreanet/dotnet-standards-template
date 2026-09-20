namespace Module.Venues.UseCases.CreateVenue;

public sealed record CreateVenueResponse(Guid Id, string VenueName, int RoomsCount);
