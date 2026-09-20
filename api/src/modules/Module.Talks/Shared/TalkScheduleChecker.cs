using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Venues;
using Shared.Http.Results;

namespace Module.Talks.Shared;

/// <summary>
/// Regras cruzadas de agenda compartilhadas por criar/atualizar palestra, resolvidas via contratos de outros módulos:
/// evento existe e aceita palestras, período dentro do evento e sala pertencente ao local do evento.
/// A sobreposição de horários na sala é verificada pelo caso de uso (consulta ao próprio schema).
/// </summary>
internal sealed class TalkScheduleChecker(
    IEventsModuleApi eventsApi,
    IVenuesModuleApi venuesApi,
    ILogger<TalkScheduleChecker> logger)
{
    private static readonly string[] StatusesThatRejectTalks = ["Closed", "Canceled"];

    public async Task<Result<EventSummary>> CheckAsync(Guid eventId, Guid trackId, Guid? roomId, DateTimeOffset talkStart, DateTimeOffset talkEnd, CancellationToken cancellationToken)
    {
        logger.LogDebug("Consultando evento {EventId} para validar agenda da palestra na trilha {TrackId}", eventId, trackId);
        var eventEntity = await eventsApi.GetEventSummaryAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            logger.LogInformation("Agenda da palestra rejeitada porque o evento {EventId} não foi encontrado", eventId);
            return TalksErrors.EventNotFound;
        }

        if (StatusesThatRejectTalks.Contains(eventEntity.EventStatus, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogInformation("Agenda da palestra rejeitada porque o evento {EventId} está na situação {EventStatus}", eventId, eventEntity.EventStatus);
            return TalksErrors.EventDoesNotAcceptTalks;
        }

        logger.LogDebug("Evento {EventId} aceita palestras; consultando trilha {TrackId}", eventId, trackId);
        var track = await eventsApi.GetTrackSummaryAsync(eventId, trackId, cancellationToken);
        if (track is null)
        {
            logger.LogInformation("Agenda da palestra rejeitada porque a trilha {TrackId} não foi encontrada no evento {EventId}", trackId, eventId);
            return TalksErrors.TrackNotFound;
        }

        if (!track.IsActive)
        {
            logger.LogInformation("Agenda da palestra rejeitada porque a trilha {TrackId} do evento {EventId} está inativa", trackId, eventId);
            return TalksErrors.TrackNotFound;
        }

        if (talkStart < eventEntity.EventStartDate || talkEnd > eventEntity.EventEndDate)
        {
            logger.LogInformation("Agenda da palestra rejeitada porque o período está fora da janela do evento {EventId}", eventId);
            return TalksErrors.PeriodOutsideEvent;
        }

        if (roomId.HasValue)
        {
            logger.LogDebug("Consultando sala {RoomId} para validar vínculo com o local do evento {EventId}", roomId.Value, eventId);
            var room = await venuesApi.GetRoomSummaryAsync(roomId.Value, cancellationToken);
            if (room is null)
            {
                logger.LogInformation("Agenda da palestra rejeitada porque a sala {RoomId} não foi encontrada", roomId.Value);
                return TalksErrors.RoomNotFound;
            }

            if (eventEntity.VenueId is null)
            {
                logger.LogInformation("Agenda da palestra rejeitada porque o evento {EventId} não possui local para receber a sala {RoomId}", eventId, roomId.Value);
                return TalksErrors.RoomDoesNotBelongToVenue;
            }

            if (room.VenueId != eventEntity.VenueId.Value)
            {
                logger.LogInformation("Agenda da palestra rejeitada porque a sala {RoomId} não pertence ao local do evento {EventId}", roomId.Value, eventId);
                return TalksErrors.RoomDoesNotBelongToVenue;
            }

            logger.LogDebug("Sala {RoomId} pertence ao local do evento {EventId}", roomId.Value, eventId);
        }
        else
        {
            logger.LogDebug("Validação da agenda da palestra não exige sala para o evento {EventId}", eventId);
        }

        logger.LogDebug("Agenda da palestra validada para o evento {EventId} e trilha {TrackId}", eventId, trackId);
        return eventEntity;
    }
}
