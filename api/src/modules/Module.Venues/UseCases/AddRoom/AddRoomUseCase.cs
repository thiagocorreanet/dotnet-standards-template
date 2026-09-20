using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.AddRoom;

[Command("event-management-example")]
internal sealed class AddRoomUseCase(VenuesDbContext db, ILogger<AddRoomUseCase> logger) : IUseCase<AddRoomRequest, AddRoomResponse>
{
    public async Task<Result<AddRoomResponse>> HandleAsync(AddRoomRequest request, CancellationToken cancellationToken)
    {
        var venue = await db.Venues
            .TagWith("Venues.AddRoom.LoadVenue")
            .Include(l => l.Rooms)
            .FirstOrDefaultAsync(l => l.Id == request.VenueId, cancellationToken);
        if (venue is null)
        {
            logger.LogInformation("Adição de sala rejeitada: local {VenueId} não encontrado", request.VenueId);
            return VenuesErrors.VenueNotFound;
        }

        logger.LogDebug("Chamando agregado Local {VenueId} para adicionar sala; salas atuais={RoomCount}", venue.Id, venue.Rooms.Count);
        var result = venue.AddRoom(request.RoomName, request.RoomCapacity, request.RoomType, request.RoomResources);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Local {VenueId} rejeitou nova sala pela regra {ErrorCode}", venue.Id, result.Error.Code);
            return result.Error;
        }

        var room = result.Value;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Sala {RoomId} adicionada ao local {VenueId}; total de salas={RoomCount}", room.Id, room.VenueId, venue.Rooms.Count);
            return Result.Success(new AddRoomResponse(room.Id, room.VenueId, room.RoomName, room.RoomCapacity, room.RoomType));
        }, cancellationToken);
    }
}
