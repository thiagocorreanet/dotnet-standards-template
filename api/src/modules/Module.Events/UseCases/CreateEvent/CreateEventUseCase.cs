using Shared.Contracts.Common;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Venues;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Events.UseCases.CreateEvent;

[Command("event-management-example")]
internal sealed class CreateEventUseCase(EventsDbContext db, IVenuesModuleApi venues, ICurrentUser user) : IUseCase<CreateEventRequest, CreateEventResponse>
{
    public async Task<Result<CreateEventResponse>> HandleAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        if (request.VenueId.HasValue)
        {
            var venue = await venues.GetVenueSummaryAsync(request.VenueId.Value, cancellationToken);
            if (venue is null)
            {
                return EventsErrors.VenueNotFound;
            }
        }

        var result = Event.Create(
            request.EventName, request.EventDescription, request.EventStartDate, request.EventEndDate,
            request.EventFormat, request.VenueId, request.EventRemoteUrl, request.EventMaximumCapacity);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var eventEntity = result.Value;
        eventEntity.SetOrganizer(user.Id ?? throw new InvalidOperationException("Identidade interna ausente."));
        var tracks = request.Tracks is { Count: > 0 }
            ? request.Tracks
            : [new CreateEventTrackRequest("Trilha única", null, "#2563EB")];
        foreach (var item in tracks)
        {
            var trackResult = eventEntity.AddTrack(item.TrackName, item.TrackDescription, item.TrackColor);
            if (trackResult.IsFailure)
            {
                return trackResult.Error;
            }
        }
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Events.Add(eventEntity);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CreateEventResponse(eventEntity.Id, eventEntity.EventName, eventEntity.EventStatus));
        }, cancellationToken);
    }
}
