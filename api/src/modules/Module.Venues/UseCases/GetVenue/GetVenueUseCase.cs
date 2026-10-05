using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Venues.UseCases.GetVenue;

internal sealed class GetVenueUseCase(VenuesDbContext db) : IUseCase<GetVenueRequest, GetVenueResponse>
{
    public async Task<Result<GetVenueResponse>> HandleAsync(GetVenueRequest request, CancellationToken cancellationToken)
    {
        var venue = await db.Venues
            .TagWith("Venues.GetVenue")
            .AsNoTracking()
            .Where(l => l.Id == request.VenueId)
            .Select(l => new GetVenueResponse(
                l.Id, l.VenueName, l.VenueDescription, l.AddressStreet, l.AddressNumber, l.AddressNeighborhood,
                l.AddressCity, l.AddressState, l.AddressPostalCode, l.Rooms.Sum(s => s.RoomCapacity), l.IsActive, l.CreatedAt, l.UpdatedAt,
                l.Rooms.OrderBy(s => s.RoomName)
                    .Select(s => new GetVenueRoomResponse(s.Id, s.RoomName, s.RoomCapacity, s.RoomType, s.RoomResources, s.IsActive))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (venue is null)
        {
            return VenuesErrors.VenueNotFound;
        }

        return venue;
    }
}
