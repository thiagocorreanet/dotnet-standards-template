using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Venues.UseCases.UpdateVenue;

[Command("event-management-example")]
internal sealed class UpdateVenueUseCase(VenuesDbContext db) : IUseCase<UpdateVenueRequest, UpdateVenueResponse>
{
    public async Task<Result<UpdateVenueResponse>> HandleAsync(UpdateVenueRequest request, CancellationToken cancellationToken)
    {
        var venue = await db.Venues
            .TagWith("Venues.UpdateVenue.Load")
            .FirstOrDefaultAsync(l => l.Id == request.VenueId, cancellationToken);
        if (venue is null)
        {
            return VenuesErrors.VenueNotFound;
        }

        var nameInUse = await db.Venues
            .TagWith("Venues.UpdateVenue.CheckName")
            .AnyAsync(l => l.Id != request.VenueId && l.VenueName == request.VenueName.Trim(), cancellationToken);
        if (nameInUse)
        {
            return VenuesErrors.DuplicateVenueName;
        }

        venue.Update(request.VenueName, request.VenueDescription, request.AddressStreet, request.AddressNumber,
            request.AddressNeighborhood, request.AddressCity, request.AddressState, request.AddressPostalCode);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new UpdateVenueResponse(venue.Id, venue.VenueName, venue.UpdatedAt));
        }, cancellationToken);
    }
}
