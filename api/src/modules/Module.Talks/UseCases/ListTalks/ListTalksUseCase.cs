using Microsoft.EntityFrameworkCore;
using Module.Talks.Shared;
using Shared.Contracts.Common;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.ListTalks;

internal sealed class ListTalksUseCase(TalksDbContext db) : IUseCase<ListTalksRequest, PagedResult<ListTalksItemResponse>>
{
    public async Task<Result<PagedResult<ListTalksItemResponse>>> HandleAsync(ListTalksRequest request, CancellationToken cancellationToken)
    {
        var query = db.Talks.TagWith("Talks.ListTalks").AsNoTracking();

        if (request.EventId.HasValue)
        {
            query = query.Where(p => p.EventId == request.EventId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.TalkTitle, search));
        }

        var descending = request.Direction == SortDirection.Desc;
        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "palestratitulo" => descending ? query.OrderByDescending(x => x.TalkTitle).ThenByDescending(x => x.Id) : query.OrderBy(x => x.TalkTitle).ThenBy(x => x.Id),
            "palestrantesquantidade" => descending ? query.OrderByDescending(x => x.Speakers.Count).ThenByDescending(x => x.Id) : query.OrderBy(x => x.Speakers.Count).ThenBy(x => x.Id),
            "presencasquantidade" => descending ? query.OrderByDescending(x => x.Attendances.Count).ThenByDescending(x => x.Id) : query.OrderBy(x => x.Attendances.Count).ThenBy(x => x.Id),
            _ => descending ? query.OrderByDescending(x => x.TalkStart).ThenByDescending(x => x.Id) : query.OrderBy(x => x.TalkStart).ThenBy(x => x.Id)
        };
        var page = await ordered
            .Select(p => new ListTalksItemResponse(p.Id, p.EventId, p.TrackId, p.RoomId, p.TalkTitle, p.TalkStart, p.TalkEnd, p.Speakers.Count, p.Attendances.Count))
            .ToPagedResultAsync(new PagedRequest(request.Page, request.PageSize), cancellationToken);

        return page;
    }
}
