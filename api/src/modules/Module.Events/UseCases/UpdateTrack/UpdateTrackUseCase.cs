using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;
namespace Module.Events.UseCases.UpdateTrack;
[Command("event-management-example")]
internal sealed class UpdateTrackUseCase(EventsDbContext db, ITalksModuleApi talks, ILogger<UpdateTrackUseCase> logger) : IUseCase<UpdateTrackRequest, UpdateTrackResponse>
{
    public async Task<Result<UpdateTrackResponse>> HandleAsync(UpdateTrackRequest request, CancellationToken ct)
    {
        if (!request.IsActive && await talks.IsTrackInUseAsync(request.TrackId, ct))
            return Error.Conflict("Events.TrackInUse", "A trilha possui palestras ativas.");
        var eventEntity = await db.Events.TagWith("Events.UpdateTrack.LoadEvent").Include(e => e.Tracks).FirstOrDefaultAsync(e => e.Id == request.EventId, ct);
        if (eventEntity is null)
        {
            logger.LogInformation("Atualização de trilha rejeitada: evento {EventId} não encontrado", request.EventId);
            return EventsErrors.EventNotFound;
        }
        logger.LogDebug("Chamando agregado Evento {EventId} para atualizar trilha {TrackId}", eventEntity.Id, request.TrackId);
        var result = eventEntity.UpdateTrack(request.TrackId, request.TrackName, request.TrackDescription, request.TrackColor);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Evento {EventId} rejeitou atualização da trilha {TrackId} pela regra {ErrorCode}", eventEntity.Id, request.TrackId, result.Error.Code);
            return result.Error;
        }
        var track = eventEntity.Tracks.First(t => t.Id == request.TrackId);
        track.IsActive = request.IsActive;
        logger.LogDebug("Estado ativo da trilha {TrackId} definido como {TrackActive}", track.Id, track.IsActive);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await db.SaveChangesAsync(token);
            logger.LogInformation("Trilha {TrackId} do evento {EventId} atualizada", track.Id, eventEntity.Id);
            return Result.Success(new UpdateTrackResponse(track.Id, track.EventId, track.TrackName, track.TrackDescription, track.TrackColor, track.IsActive));
        }, ct);
    }
}
