using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;
namespace Module.Events.UseCases.ListTracks;
internal sealed class ListTracksUseCase(EventsDbContext db) : IUseCase<ListTracksRequest, IReadOnlyList<ListTracksItemResponse>>
{
    public async Task<Result<IReadOnlyList<ListTracksItemResponse>>> HandleAsync(ListTracksRequest request, CancellationToken ct)
    {
        if (!await db.Events.TagWith("Events.ListTracks.CheckEvent").AnyAsync(e => e.Id == request.EventId, ct))
        {
            return EventsErrors.EventNotFound;
        }
        var query = db.Tracks.TagWith("Events.ListTracks").AsNoTracking().Where(t => t.EventId == request.EventId);
        if (request.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == request.IsActive.Value);
        }
        var tracks = await query.OrderBy(t => t.TrackName).Select(t => new ListTracksItemResponse(t.Id, t.EventId, t.TrackName, t.TrackDescription, t.TrackColor, t.IsActive)).ToListAsync(ct);
        return tracks;
    }
}
