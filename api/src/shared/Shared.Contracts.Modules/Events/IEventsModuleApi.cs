using Shared.Contracts.Integration;

namespace Shared.Contracts.Events;

/// <summary>Contrato síncrono do módulo Eventos (consumido por Palestras para validar período, local e inscrição).</summary>
public interface IEventsModuleApi
{
    Task<bool> IsVenueInUseAsync(Guid venueId, CancellationToken ct);
    Task<bool> BelongsToOrganizerAsync(Guid eventId, Guid userId, CancellationToken ct);
    Task<EventSummary?> GetEventSummaryAsync(Guid eventId, CancellationToken cancellationToken);
    Task<bool> ConfirmedRegistrationExistsAsync(Guid eventId, Guid personId, CancellationToken cancellationToken);
    Task<TrackSummary?> GetTrackSummaryAsync(Guid eventId, Guid trackId, CancellationToken cancellationToken);
}

public sealed record EventSummary(
    Guid Id,
    string EventName,
    DateTimeOffset EventStartDate,
    DateTimeOffset EventEndDate,
    string EventFormat,
    string EventStatus,
    Guid? VenueId);

public sealed record TrackSummary(Guid Id, Guid EventId, string TrackName, bool IsActive);

[EventContract("events.event-published.v1", requiresConsumer: false)]
public sealed record EventPublished(Guid EventId) : IntegrationEvent;
[EventContract("events.event-canceled.v1", requiresConsumer: false)]
public sealed record EventCanceled(Guid EventId) : IntegrationEvent;
[EventContract("events.registration-completed.v1", requiresConsumer: false)]
public sealed record RegistrationCompleted(Guid RegistrationId, Guid EventId, Guid PersonId) : IntegrationEvent;
[EventContract("events.registration-canceled.v1", requiresConsumer: false)]
public sealed record RegistrationCanceled(Guid RegistrationId, Guid EventId, Guid PersonId) : IntegrationEvent;
