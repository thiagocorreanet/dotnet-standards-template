using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.GetVenue;

internal sealed class GetVenueUseCase(VenuesDbContext db, ILogger<GetVenueUseCase> logger) : IUseCase<GetVenueRequest, GetVenueResponse>
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
            logger.LogInformation("Local {VenueId} não encontrado para detalhamento", request.VenueId);
            return VenuesErrors.VenueNotFound;
        }

        logger.LogInformation("Local {VenueId} carregado com {RoomCount} sala(s) e capacidade total {TotalCapacity}", venue.Id, venue.Rooms.Count, venue.VenueTotalCapacity);
        return venue;
    }
}
