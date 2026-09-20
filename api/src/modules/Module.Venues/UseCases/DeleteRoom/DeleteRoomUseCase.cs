using Shared.Contracts.Events;
using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.DeleteRoom;

[Command("event-management-example")]
internal sealed class DeleteRoomUseCase(VenuesDbContext db, IEventsModuleApi events, ITalksModuleApi talks, ILogger<DeleteRoomUseCase> logger) : IUseCase<DeleteRoomRequest, DeleteRoomResponse>
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
            logger.LogInformation("Exclusão de sala rejeitada: local {VenueId} não encontrado", request.VenueId);
            return VenuesErrors.VenueNotFound;
        }

        logger.LogDebug("Chamando agregado Local {VenueId} para remover sala {RoomId}", venue.Id, request.RoomId);
        var result = venue.RemoveRoom(request.RoomId);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Local {VenueId} rejeitou remoção da sala {RoomId} pela regra {ErrorCode}", venue.Id, request.RoomId, result.Error.Code);
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Rooms.Remove(result.Value);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Sala {RoomId} do local {VenueId} excluída logicamente", result.Value.Id, venue.Id);
            return Result.Success(new DeleteRoomResponse(result.Value.Id));
        }, cancellationToken);
    }
}
