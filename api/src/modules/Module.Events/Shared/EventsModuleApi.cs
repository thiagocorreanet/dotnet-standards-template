using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Shared.Contracts.Events;

namespace Module.Events.Shared;

/// <summary>Implementação do contrato síncrono consumido por Palestras. Projeções mínimas, sem tracking; enums como texto.</summary>
internal sealed class EventsModuleApi(EventsDbContext db) : IEventsModuleApi
{
    public Task<bool> IsVenueInUseAsync(Guid venueId, CancellationToken ct) =>
        db.Events.AnyAsync(e => e.VenueId == venueId && e.IsActive, ct);
    public Task<bool> BelongsToOrganizerAsync(Guid eventId, Guid userId, CancellationToken ct) =>
        db.Events.AnyAsync(e => e.Id == eventId && e.OrganizerId == userId && e.IsActive, ct);
    public Task<EventSummary?> GetEventSummaryAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.Events
            .TagWith("Events.ModuleApi.GetEventSummary")
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new EventSummary(
                e.Id, e.EventName, e.EventStartDate, e.EventEndDate, e.EventFormat.ToString(), e.EventStatus.ToString(), e.VenueId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ConfirmedRegistrationExistsAsync(Guid eventId, Guid personId, CancellationToken cancellationToken) =>
        db.Registrations
            .TagWith("Events.ModuleApi.RegistrationConfirmedExists")
            .AsNoTracking()
            .AnyAsync(i => i.EventId == eventId && i.PersonId == personId && i.RegistrationStatus == RegistrationStatus.Confirmed, cancellationToken);

    public Task<TrackSummary?> GetTrackSummaryAsync(Guid eventId, Guid trackId, CancellationToken cancellationToken) =>
        db.Tracks.TagWith("Events.ModuleApi.GetTrackSummary").AsNoTracking()
            .Where(t => t.Id == trackId && t.EventId == eventId)
            .Select(t => new TrackSummary(t.Id, t.EventId, t.TrackName, t.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
}
