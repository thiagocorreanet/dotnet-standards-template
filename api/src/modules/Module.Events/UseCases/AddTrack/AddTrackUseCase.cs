using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;
namespace Module.Events.UseCases.AddTrack;
[Command("event-management-example")]
internal sealed class AddTrackUseCase(EventsDbContext db) : IUseCase<AddTrackRequest, AddTrackResponse>
{
    public async Task<Result<AddTrackResponse>> HandleAsync(AddTrackRequest request, CancellationToken ct)
    {
        var eventEntity = await db.Events.TagWith("Events.AddTrack.LoadEvent").Include(e => e.Tracks).FirstOrDefaultAsync(e => e.Id == request.EventId, ct);
        if (eventEntity is null)
        {
            return EventsErrors.EventNotFound;
        }
        var result = eventEntity.AddTrack(request.TrackName, request.TrackDescription, request.TrackColor);
        if (result.IsFailure)
        {
            return result.Error;
        }
        var track = result.Value;
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await db.SaveChangesAsync(token);
            return Result.Success(new AddTrackResponse(track.Id, track.EventId, track.TrackName, track.TrackDescription, track.TrackColor));
        }, ct);
    }
}
