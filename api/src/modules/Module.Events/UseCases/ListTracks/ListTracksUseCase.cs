using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Http.Endpoints;
using Shared.Http.Results;
namespace Module.Events.UseCases.ListTracks;
internal sealed class ListTracksUseCase(EventsDbContext db, ILogger<ListTracksUseCase> logger) : IUseCase<ListTracksRequest, IReadOnlyList<ListTracksItemResponse>>
{
    public async Task<Result<IReadOnlyList<ListTracksItemResponse>>> HandleAsync(ListTracksRequest request, CancellationToken ct)
    {
        if (!await db.Events.TagWith("Events.ListTracks.CheckEvent").AnyAsync(e => e.Id == request.EventId, ct))
        {
            logger.LogInformation("Listagem de trilhas rejeitada: evento {EventId} não encontrado", request.EventId);
            return EventsErrors.EventNotFound;
        }
        var query = db.Tracks.TagWith("Events.ListTracks").AsNoTracking().Where(t => t.EventId == request.EventId);
        if (request.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == request.IsActive.Value);
            logger.LogDebug("Filtro de ativo={Active} aplicado às trilhas do evento {EventId}", request.IsActive, request.EventId);
        }
        else
        {
            logger.LogDebug("Listagem de trilhas do evento {EventId} inclui ativas e inativas", request.EventId);
        }
        var tracks = await query.OrderBy(t => t.TrackName).Select(t => new ListTracksItemResponse(t.Id, t.EventId, t.TrackName, t.TrackDescription, t.TrackColor, t.IsActive)).ToListAsync(ct);
        logger.LogInformation("Evento {EventId} possui {TrackCount} trilha(s) no filtro solicitado", request.EventId, tracks.Count);
        return tracks;
    }
}
