using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.CreateVenue;

[Command("event-management-example")]
internal sealed class CreateVenueUseCase(VenuesDbContext db, ILogger<CreateVenueUseCase> logger) : IUseCase<CreateVenueRequest, CreateVenueResponse>
{
    public async Task<Result<CreateVenueResponse>> HandleAsync(CreateVenueRequest request, CancellationToken cancellationToken)
    {
        var nameInUse = await db.Venues
            .TagWith("Venues.CreateVenue.CheckName")
            .AnyAsync(l => l.VenueName == request.VenueName.Trim(), cancellationToken);
        if (nameInUse)
        {
            logger.LogInformation("Criação de local rejeitada porque o nome normalizado já está em uso");
            return VenuesErrors.DuplicateVenueName;
        }

        logger.LogDebug("Chamando fábrica de domínio Local.Criar; ambiente único={SingleEnvironment}", request.SingleRoomCapacity.HasValue);
        var venue = Venue.Create(
            request.VenueName, request.VenueDescription, request.AddressStreet, request.AddressNumber,
            request.AddressNeighborhood, request.AddressCity, request.AddressState, request.AddressPostalCode, request.SingleRoomCapacity);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Venues.Add(venue);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Local {VenueId} criado com {RoomCount} sala(s)", venue.Id, venue.Rooms.Count);
            return Result.Success(new CreateVenueResponse(venue.Id, venue.VenueName, venue.Rooms.Count));
        }, cancellationToken);
    }
}
