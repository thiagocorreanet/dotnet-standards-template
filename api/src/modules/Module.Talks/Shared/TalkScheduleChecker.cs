using Module.Talks.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Venues;
using Shared.Kernel.Results;

namespace Module.Talks.Shared;

/// <summary>
/// Regras cruzadas de agenda compartilhadas por criar/atualizar palestra, resolvidas via contratos de outros módulos:
/// evento existe e aceita palestras, período dentro do evento e sala pertencente ao local do evento.
/// A sobreposição de horários na sala é verificada pelo caso de uso (consulta ao próprio schema).
/// </summary>
internal sealed class TalkScheduleChecker(
    IEventsModuleApi eventsApi,
    IVenuesModuleApi venuesApi)
{
    private static readonly string[] StatusesThatRejectTalks = ["Closed", "Canceled"];

    public async Task<Result<EventSummary>> CheckAsync(Guid eventId, Guid trackId, Guid? roomId, DateTimeOffset talkStart, DateTimeOffset talkEnd, CancellationToken cancellationToken)
    {
        var eventEntity = await eventsApi.GetEventSummaryAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            return TalksErrors.EventNotFound;
        }

        if (StatusesThatRejectTalks.Contains(eventEntity.EventStatus, StringComparer.OrdinalIgnoreCase))
        {
            return TalksErrors.EventDoesNotAcceptTalks;
        }

        var track = await eventsApi.GetTrackSummaryAsync(eventId, trackId, cancellationToken);
        if (track is null)
        {
            return TalksErrors.TrackNotFound;
        }

        if (!track.IsActive)
        {
            return TalksErrors.TrackNotFound;
        }

        if (talkStart < eventEntity.EventStartDate || talkEnd > eventEntity.EventEndDate)
        {
            return TalksErrors.PeriodOutsideEvent;
        }

        if (roomId.HasValue)
        {
            var room = await venuesApi.GetRoomSummaryAsync(roomId.Value, cancellationToken);
            if (room is null)
            {
                return TalksErrors.RoomNotFound;
            }

            if (eventEntity.VenueId is null)
            {
                return TalksErrors.RoomDoesNotBelongToVenue;
            }

            if (room.VenueId != eventEntity.VenueId.Value)
            {
                return TalksErrors.RoomDoesNotBelongToVenue;
            }
        }

        return eventEntity;
    }
}
