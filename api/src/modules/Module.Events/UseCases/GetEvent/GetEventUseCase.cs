using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Venues;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Events.UseCases.GetEvent;

internal sealed class GetEventUseCase(EventsDbContext db, IVenuesModuleApi venues, ILogger<GetEventUseCase> logger) : IUseCase<GetEventRequest, GetEventResponse>
{
    public async Task<Result<GetEventResponse>> HandleAsync(GetEventRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.GetEvent")
            .AsNoTracking()
            .Where(e => e.Id == request.EventId)
            .Select(e => new GetEventResponse(
                e.Id, e.EventName, e.EventDescription, e.EventStartDate, e.EventEndDate, e.EventFormat, e.EventStatus,
                e.VenueId, null, e.EventRemoteUrl, e.EventMaximumCapacity,
                e.Registrations.Count(i => i.RegistrationStatus == RegistrationStatus.Confirmed),
                e.EventCancellationReason, e.CreatedAt, e.UpdatedAt,
                e.Tracks.OrderBy(t => t.TrackName).Select(t => new GetEventTrackResponse(t.Id, t.TrackName, t.TrackDescription, t.TrackColor, t.IsActive)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
        if (eventEntity is null)
        {
            return EventsErrors.EventNotFound;
        }

        if (eventEntity.VenueId is null)
        {
            return eventEntity;
        }

        var venue = await venues.GetVenueSummaryAsync(eventEntity.VenueId.Value, cancellationToken);
        if (venue is null)
        {
            logger.LogWarning("Local {VenueId} referenciado pelo evento {EventId} não foi encontrado durante o enriquecimento", eventEntity.VenueId, eventEntity.Id);
        }
        return eventEntity with { VenueName = venue?.VenueName };
    }
}
