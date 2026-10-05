using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Venues.UseCases.ListRooms;

internal sealed class ListRoomsUseCase(VenuesDbContext db) : IUseCase<ListRoomsRequest, IReadOnlyList<ListRoomsItemResponse>>
{
    public async Task<Result<IReadOnlyList<ListRoomsItemResponse>>> HandleAsync(ListRoomsRequest request, CancellationToken cancellationToken)
    {
        var venueExists = await db.Venues.TagWith("Venues.ListRooms.CheckVenue").AnyAsync(l => l.Id == request.VenueId, cancellationToken);
        if (!venueExists)
        {
            return VenuesErrors.VenueNotFound;
        }

        var query = db.Rooms.TagWith("Venues.ListRooms").AsNoTracking().Where(s => s.VenueId == request.VenueId);
        if (request.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActive.Value);
        }

        var rooms = await query
            .OrderBy(s => s.RoomName)
            .Select(s => new ListRoomsItemResponse(s.Id, s.RoomName, s.RoomCapacity, s.RoomType, s.RoomResources, s.IsActive))
            .ToListAsync(cancellationToken);

        return rooms;
    }
}
