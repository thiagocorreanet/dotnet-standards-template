using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;
namespace Module.Events.UseCases.UpdateTrack;
[Command("event-management-example")]
internal sealed class UpdateTrackUseCase(EventsDbContext db, ITalksModuleApi talks) : IUseCase<UpdateTrackRequest, UpdateTrackResponse>
{
    public async Task<Result<UpdateTrackResponse>> HandleAsync(UpdateTrackRequest request, CancellationToken ct)
    {
        if (!request.IsActive && await talks.IsTrackInUseAsync(request.TrackId, ct))
            return Error.Conflict("Events.TrackInUse", "A trilha possui palestras ativas.");
        var eventEntity = await db.Events.TagWith("Events.UpdateTrack.LoadEvent").Include(e => e.Tracks).FirstOrDefaultAsync(e => e.Id == request.EventId, ct);
        if (eventEntity is null)
        {
            return EventsErrors.EventNotFound;
        }
        var result = eventEntity.UpdateTrack(request.TrackId, request.TrackName, request.TrackDescription, request.TrackColor);
        if (result.IsFailure)
        {
            return result.Error;
        }
        var track = eventEntity.Tracks.First(t => t.Id == request.TrackId);
        track.IsActive = request.IsActive;
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await db.SaveChangesAsync(token);
            return Result.Success(new UpdateTrackResponse(track.Id, track.EventId, track.TrackName, track.TrackDescription, track.TrackColor, track.IsActive));
        }, ct);
    }
}
