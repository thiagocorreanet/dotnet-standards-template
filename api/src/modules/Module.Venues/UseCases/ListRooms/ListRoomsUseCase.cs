using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.ListRooms;

internal sealed class ListRoomsUseCase(VenuesDbContext db, ILogger<ListRoomsUseCase> logger) : IUseCase<ListRoomsRequest, IReadOnlyList<ListRoomsItemResponse>>
{
    public async Task<Result<IReadOnlyList<ListRoomsItemResponse>>> HandleAsync(ListRoomsRequest request, CancellationToken cancellationToken)
    {
        var venueExists = await db.Venues.TagWith("Venues.ListRooms.CheckVenue").AnyAsync(l => l.Id == request.VenueId, cancellationToken);
        if (!venueExists)
        {
            logger.LogInformation("Listagem de salas rejeitada: local {VenueId} não encontrado", request.VenueId);
            return VenuesErrors.VenueNotFound;
        }

        var query = db.Rooms.TagWith("Venues.ListRooms").AsNoTracking().Where(s => s.VenueId == request.VenueId);
        if (request.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActive.Value);
            logger.LogDebug("Filtro de ativo={Active} aplicado às salas do local {VenueId}", request.IsActive, request.VenueId);
        }
        else
        {
            logger.LogDebug("Listagem de salas do local {VenueId} inclui ativas e inativas", request.VenueId);
        }

        var rooms = await query
            .OrderBy(s => s.RoomName)
            .Select(s => new ListRoomsItemResponse(s.Id, s.RoomName, s.RoomCapacity, s.RoomType, s.RoomResources, s.IsActive))
            .ToListAsync(cancellationToken);

        logger.LogInformation("Local {VenueId} possui {RoomCount} sala(s) no filtro solicitado", request.VenueId, rooms.Count);

        return rooms;
    }
}
