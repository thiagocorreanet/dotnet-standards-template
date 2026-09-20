using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;
namespace Module.Events.UseCases.AddTrack;
[Command("event-management-example")]
internal sealed class AddTrackUseCase(EventsDbContext db, ILogger<AddTrackUseCase> logger) : IUseCase<AddTrackRequest, AddTrackResponse>
{
    public async Task<Result<AddTrackResponse>> HandleAsync(AddTrackRequest request, CancellationToken ct)
    {
        var eventEntity = await db.Events.TagWith("Events.AddTrack.LoadEvent").Include(e => e.Tracks).FirstOrDefaultAsync(e => e.Id == request.EventId, ct);
        if (eventEntity is null)
        {
            logger.LogInformation("Adição de trilha rejeitada: evento {EventId} não encontrado", request.EventId);
            return EventsErrors.EventNotFound;
        }
        logger.LogDebug("Chamando agregado Evento {EventId} para adicionar trilha; trilhas atuais={TrackCount}", eventEntity.Id, eventEntity.Tracks.Count);
        var result = eventEntity.AddTrack(request.TrackName, request.TrackDescription, request.TrackColor);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Evento {EventId} rejeitou nova trilha pela regra {ErrorCode}", eventEntity.Id, result.Error.Code);
            return result.Error;
        }
        var track = result.Value;
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await db.SaveChangesAsync(token);
            logger.LogInformation("Trilha {TrackId} adicionada ao evento {EventId}; total de trilhas={TrackCount}", track.Id, eventEntity.Id, eventEntity.Tracks.Count);
            return Result.Success(new AddTrackResponse(track.Id, track.EventId, track.TrackName, track.TrackDescription, track.TrackColor));
        }, ct);
    }
}
