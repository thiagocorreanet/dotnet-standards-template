using Shared.Contracts.Events;
using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Venues.UseCases.DeleteRoom;

[Command("event-management-example")]
internal sealed class DeleteRoomUseCase(VenuesDbContext db, IEventsModuleApi events, ITalksModuleApi talks) : IUseCase<DeleteRoomRequest, DeleteRoomResponse>
{
    public async Task<Result<DeleteRoomResponse>> HandleAsync(DeleteRoomRequest request, CancellationToken cancellationToken)
    {
        if (await events.IsVenueInUseAsync(request.VenueId, cancellationToken) || await talks.IsRoomInUseAsync(request.RoomId, cancellationToken))
            return Error.Conflict("Venues.ResourceInUse", "Altere as referências antes de modificar ou excluir este recurso.");
        var venue = await db.Venues
            .TagWith("Venues.DeleteRoom.LoadVenue")
            .Include(l => l.Rooms)
            .FirstOrDefaultAsync(l => l.Id == request.VenueId, cancellationToken);
        if (venue is null)
        {
            return VenuesErrors.VenueNotFound;
        }

        var result = venue.RemoveRoom(request.RoomId);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Rooms.Remove(result.Value);
            await db.SaveChangesAsync(ct);
            return Result.Success(new DeleteRoomResponse(result.Value.Id));
        }, cancellationToken);
    }
}
