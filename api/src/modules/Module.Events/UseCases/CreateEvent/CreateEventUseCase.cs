using Shared.Contracts.Common;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Venues;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.CreateEvent;

[Command("event-management-example")]
internal sealed class CreateEventUseCase(EventsDbContext db, IVenuesModuleApi venues, ICurrentUser user, ILogger<CreateEventUseCase> logger) : IUseCase<CreateEventRequest, CreateEventResponse>
{
    public async Task<Result<CreateEventResponse>> HandleAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        if (request.VenueId.HasValue)
        {
            logger.LogDebug("Consultando módulo Locais para validar o local {VenueId} do novo evento", request.VenueId);
            var venue = await venues.GetVenueSummaryAsync(request.VenueId.Value, cancellationToken);
            if (venue is null)
            {
                logger.LogInformation("Criação do evento impedida porque o local {VenueId} não existe", request.VenueId);
                return EventsErrors.VenueNotFound;
            }
        }
        else
        {
            logger.LogDebug("Novo evento não referencia local; validação no módulo Locais não é necessária");
        }

        logger.LogDebug("Chamando fábrica de domínio Evento.Criar com formato {EventFormat}", request.EventFormat);
        var result = Event.Create(
            request.EventName, request.EventDescription, request.EventStartDate, request.EventEndDate,
            request.EventFormat, request.VenueId, request.EventRemoteUrl, request.EventMaximumCapacity);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado rejeitou a criação do evento pela regra {ErrorCode}", result.Error.Code);
            return result.Error;
        }

        var eventEntity = result.Value;
        eventEntity.SetOrganizer(user.Id ?? throw new InvalidOperationException("Identidade interna ausente."));
        var tracks = request.Tracks is { Count: > 0 }
            ? request.Tracks
            : [new CreateEventTrackRequest("Trilha única", null, "#2563EB")];
        logger.LogInformation("Montando evento {EventId} com {TrackCount} trilha(s); trilha padrão aplicada={DefaultTrackApplied}",
            eventEntity.Id, tracks.Count, request.Tracks is not { Count: > 0 });
        foreach (var item in tracks)
        {
            logger.LogDebug("Adicionando trilha {TrackIndex} de {TrackCount} ao evento {EventId}", eventEntity.Tracks.Count + 1, tracks.Count, eventEntity.Id);
            var trackResult = eventEntity.AddTrack(item.TrackName, item.TrackDescription, item.TrackColor);
            if (trackResult.IsFailure)
            {
                logger.LogInformation("Agregado rejeitou uma trilha do evento {EventId} pela regra {ErrorCode}", eventEntity.Id, trackResult.Error.Code);
                return trackResult.Error;
            }
            logger.LogDebug("Trilha {TrackId} adicionada em memória ao evento {EventId}", trackResult.Value.Id, eventEntity.Id);
        }
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Events.Add(eventEntity);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Evento {EventId} persistido em situação {EventStatus} com {TrackCount} trilha(s)", eventEntity.Id, eventEntity.EventStatus, eventEntity.Tracks.Count);
            return Result.Success(new CreateEventResponse(eventEntity.Id, eventEntity.EventName, eventEntity.EventStatus));
        }, cancellationToken);
    }
}
