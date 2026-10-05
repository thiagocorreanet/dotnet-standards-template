using Shared.Contracts.Events;
using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Venues.UseCases.UpdateRoom;

[Command("event-management-example")]
internal sealed class UpdateRoomUseCase(VenuesDbContext db, IEventsModuleApi events, ITalksModuleApi talks) : IUseCase<UpdateRoomRequest, UpdateRoomResponse>
{
    public async Task<Result<UpdateRoomResponse>> HandleAsync(UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        if (await events.IsVenueInUseAsync(request.VenueId, cancellationToken) || await talks.IsRoomInUseAsync(request.RoomId, cancellationToken))
            return Error.Conflict("Venues.ResourceInUse", "Altere as referências antes de modificar ou excluir este recurso.");
        var venue = await db.Venues
            .TagWith("Venues.UpdateRoom.LoadVenue")
            .Include(l => l.Rooms)
            .FirstOrDefaultAsync(l => l.Id == request.VenueId, cancellationToken);
        if (venue is null)
        {
            return VenuesErrors.VenueNotFound;
        }

        var result = venue.UpdateRoom(request.RoomId, request.RoomName, request.RoomCapacity, request.RoomType, request.RoomResources);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var room = venue.Rooms.First(s => s.Id == request.RoomId);
        room.IsActive = request.IsActive;

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new UpdateRoomResponse(room.Id, room.VenueId, room.RoomName, room.RoomCapacity, room.RoomType, room.IsActive));
        }, cancellationToken);
    }
}
