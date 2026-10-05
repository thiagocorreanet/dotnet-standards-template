using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Common;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Events.UseCases.ListEvents;

internal sealed class ListEventsUseCase(EventsDbContext db) : IUseCase<ListEventsRequest, PagedResult<ListEventsItemResponse>>
{
    public async Task<Result<PagedResult<ListEventsItemResponse>>> HandleAsync(ListEventsRequest request, CancellationToken cancellationToken)
    {
        var query = db.Events.TagWith("Events.ListEvents").AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.EventName, search) || (e.EventDescription != null && EF.Functions.ILike(e.EventDescription, search)));
        }

        if (request.EventStatus.HasValue)
        {
            query = query.Where(e => e.EventStatus == request.EventStatus.Value);
        }

        if (request.EventFormat.HasValue)
        {
            query = query.Where(e => e.EventFormat == request.EventFormat.Value);
        }

        if (request.StartDateFrom.HasValue)
        {
            query = query.Where(e => e.EventStartDate >= request.StartDateFrom.Value);
        }

        if (request.StartDateUntil.HasValue)
        {
            query = query.Where(e => e.EventStartDate <= request.StartDateUntil.Value);
        }

        var descending = request.Direction == SortDirection.Desc;
        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "eventonome" => descending ? query.OrderByDescending(x => x.EventName).ThenByDescending(x => x.Id) : query.OrderBy(x => x.EventName).ThenBy(x => x.Id),
            "eventoformato" => descending ? query.OrderByDescending(x => x.EventFormat).ThenByDescending(x => x.Id) : query.OrderBy(x => x.EventFormat).ThenBy(x => x.Id),
            "eventosituacao" => descending ? query.OrderByDescending(x => x.EventStatus).ThenByDescending(x => x.Id) : query.OrderBy(x => x.EventStatus).ThenBy(x => x.Id),
            "inscricoesconfirmadas" => descending ? query.OrderByDescending(x => x.Registrations.Count(i => i.RegistrationStatus == RegistrationStatus.Confirmed)).ThenByDescending(x => x.Id) : query.OrderBy(x => x.Registrations.Count(i => i.RegistrationStatus == RegistrationStatus.Confirmed)).ThenBy(x => x.Id),
            _ => descending ? query.OrderByDescending(x => x.EventStartDate).ThenByDescending(x => x.Id) : query.OrderBy(x => x.EventStartDate).ThenBy(x => x.Id)
        };
        var page = await ordered
            .Select(e => new ListEventsItemResponse(e.Id, e.EventName, e.EventStartDate, e.EventEndDate, e.EventFormat, e.EventStatus, e.VenueId, e.Registrations.Count(i => i.RegistrationStatus == RegistrationStatus.Confirmed)))
            .ToPagedResultAsync(new PagedRequest(request.Page, request.PageSize), cancellationToken);

        return page;
    }
}
