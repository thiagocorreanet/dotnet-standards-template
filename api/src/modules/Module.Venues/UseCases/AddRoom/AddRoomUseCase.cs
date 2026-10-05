using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Venues.UseCases.AddRoom;

[Command("event-management-example")]
internal sealed class AddRoomUseCase(VenuesDbContext db) : IUseCase<AddRoomRequest, AddRoomResponse>
{
    public async Task<Result<AddRoomResponse>> HandleAsync(AddRoomRequest request, CancellationToken cancellationToken)
    {
        var venue = await db.Venues
            .TagWith("Venues.AddRoom.LoadVenue")
            .Include(l => l.Rooms)
            .FirstOrDefaultAsync(l => l.Id == request.VenueId, cancellationToken);
        if (venue is null)
        {
            return VenuesErrors.VenueNotFound;
        }

        var result = venue.AddRoom(request.RoomName, request.RoomCapacity, request.RoomType, request.RoomResources);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var room = result.Value;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new AddRoomResponse(room.Id, room.VenueId, room.RoomName, room.RoomCapacity, room.RoomType));
        }, cancellationToken);
    }
}
