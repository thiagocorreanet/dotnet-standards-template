using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;
namespace Module.Events.UseCases.DeleteTrack;
[Command("event-management-example")]
internal sealed class DeleteTrackUseCase(EventsDbContext db, ITalksModuleApi talks, ILogger<DeleteTrackUseCase> logger) : IUseCase<DeleteTrackRequest, DeleteTrackResponse>
{
    public async Task<Result<DeleteTrackResponse>> HandleAsync(DeleteTrackRequest request, CancellationToken ct)
    {
        if (await talks.IsTrackInUseAsync(request.TrackId, ct))
            return Error.Conflict("Events.TrackInUse", "A trilha possui palestras ativas.");
        var eventEntity = await db.Events.TagWith("Events.DeleteTrack.LoadEvent").Include(e => e.Tracks).FirstOrDefaultAsync(e => e.Id == request.EventId, ct);
        if (eventEntity is null)
        {
            logger.LogInformation("Exclusão de trilha rejeitada: evento {EventId} não encontrado", request.EventId);
            return EventsErrors.EventNotFound;
        }
        logger.LogDebug("Chamando agregado Evento {EventId} para remover trilha {TrackId}; trilhas atuais={TrackCount}", eventEntity.Id, request.TrackId, eventEntity.Tracks.Count);
        var result = eventEntity.RemoveTrack(request.TrackId);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Evento {EventId} rejeitou remoção da trilha {TrackId} pela regra {ErrorCode}", eventEntity.Id, request.TrackId, result.Error.Code);
            return result.Error;
        }
        return await db.ExecuteInTransactionAsync(async token =>
        {
            db.Tracks.Remove(result.Value);
            await db.SaveChangesAsync(token);
            logger.LogInformation("Trilha {TrackId} removida do evento {EventId}; trilhas restantes={TrackCount}", result.Value.Id, eventEntity.Id, eventEntity.Tracks.Count);
            return Result.Success(new DeleteTrackResponse(result.Value.Id));
        }, ct);
    }
}
