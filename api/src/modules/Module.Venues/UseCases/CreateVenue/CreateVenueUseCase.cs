using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Venues.UseCases.CreateVenue;

[Command("event-management-example")]
internal sealed class CreateVenueUseCase(VenuesDbContext db) : IUseCase<CreateVenueRequest, CreateVenueResponse>
{
    public async Task<Result<CreateVenueResponse>> HandleAsync(CreateVenueRequest request, CancellationToken cancellationToken)
    {
        var nameInUse = await db.Venues
            .TagWith("Venues.CreateVenue.CheckName")
            .AnyAsync(l => l.VenueName == request.VenueName.Trim(), cancellationToken);
        if (nameInUse)
        {
            return VenuesErrors.DuplicateVenueName;
        }

        var venue = Venue.Create(
            request.VenueName, request.VenueDescription, request.AddressStreet, request.AddressNumber,
            request.AddressNeighborhood, request.AddressCity, request.AddressState, request.AddressPostalCode, request.SingleRoomCapacity);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Venues.Add(venue);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CreateVenueResponse(venue.Id, venue.VenueName, venue.Rooms.Count));
        }, cancellationToken);
    }
}
