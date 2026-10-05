using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;
namespace Module.Events.UseCases.DeleteTrack;
[Command("event-management-example")]
internal sealed class DeleteTrackUseCase(EventsDbContext db, ITalksModuleApi talks) : IUseCase<DeleteTrackRequest, DeleteTrackResponse>
{
    public async Task<Result<DeleteTrackResponse>> HandleAsync(DeleteTrackRequest request, CancellationToken ct)
    {
        if (await talks.IsTrackInUseAsync(request.TrackId, ct))
            return Error.Conflict("Events.TrackInUse", "A trilha possui palestras ativas.");
        var eventEntity = await db.Events.TagWith("Events.DeleteTrack.LoadEvent").Include(e => e.Tracks).FirstOrDefaultAsync(e => e.Id == request.EventId, ct);
        if (eventEntity is null)
        {
            return EventsErrors.EventNotFound;
        }
        var result = eventEntity.RemoveTrack(request.TrackId);
        if (result.IsFailure)
        {
            return result.Error;
        }
        return await db.ExecuteInTransactionAsync(async token =>
        {
            db.Tracks.Remove(result.Value);
            await db.SaveChangesAsync(token);
            return Result.Success(new DeleteTrackResponse(result.Value.Id));
        }, ct);
    }
}
