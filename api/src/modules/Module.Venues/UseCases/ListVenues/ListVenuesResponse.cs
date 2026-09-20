namespace Module.Venues.UseCases.ListVenues;

public sealed record ListVenuesItemResponse(Guid Id, string VenueName, string AddressCity, string AddressState, int RoomsCount, int VenueTotalCapacity, bool IsActive);
