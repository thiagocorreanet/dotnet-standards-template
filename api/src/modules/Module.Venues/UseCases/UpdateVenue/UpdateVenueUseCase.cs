using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.UpdateVenue;

[Command("event-management-example")]
internal sealed class UpdateVenueUseCase(VenuesDbContext db, ILogger<UpdateVenueUseCase> logger) : IUseCase<UpdateVenueRequest, UpdateVenueResponse>
{
    public async Task<Result<UpdateVenueResponse>> HandleAsync(UpdateVenueRequest request, CancellationToken cancellationToken)
    {
        var venue = await db.Venues
            .TagWith("Venues.UpdateVenue.Load")
            .FirstOrDefaultAsync(l => l.Id == request.VenueId, cancellationToken);
        if (venue is null)
        {
            logger.LogInformation("Local {VenueId} não encontrado para atualização", request.VenueId);
            return VenuesErrors.VenueNotFound;
        }

        var nameInUse = await db.Venues
            .TagWith("Venues.UpdateVenue.CheckName")
            .AnyAsync(l => l.Id != request.VenueId && l.VenueName == request.VenueName.Trim(), cancellationToken);
        if (nameInUse)
        {
            logger.LogInformation("Atualização do local {VenueId} rejeitada porque o nome normalizado já está em uso", venue.Id);
            return VenuesErrors.DuplicateVenueName;
        }

        logger.LogDebug("Chamando agregado Local {VenueId} para atualizar dados cadastrais", venue.Id);
        venue.Update(request.VenueName, request.VenueDescription, request.AddressStreet, request.AddressNumber,
            request.AddressNeighborhood, request.AddressCity, request.AddressState, request.AddressPostalCode);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Local {VenueId} atualizado", venue.Id);
            return Result.Success(new UpdateVenueResponse(venue.Id, venue.VenueName, venue.UpdatedAt));
        }, cancellationToken);
    }
}
