using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Venues.Shared;
using Shared.Contracts.Common;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.ListVenues;

internal sealed class ListVenuesUseCase(VenuesDbContext db, ILogger<ListVenuesUseCase> logger) : IUseCase<ListVenuesRequest, PagedResult<ListVenuesItemResponse>>
{
    public async Task<Result<PagedResult<ListVenuesItemResponse>>> HandleAsync(ListVenuesRequest request, CancellationToken cancellationToken)
    {
        var query = db.Venues.TagWith("Venues.ListVenues").AsNoTracking();
        logger.LogDebug("Montando listagem de locais: busca={HasSearch}, UF={HasState}, ativo={HasActiveFilter}",
            !string.IsNullOrWhiteSpace(request.Search), !string.IsNullOrWhiteSpace(request.AddressState), request.IsActive.HasValue);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            query = query.Where(l => EF.Functions.ILike(l.VenueName, search) || EF.Functions.ILike(l.AddressCity, search));
            logger.LogDebug("Filtro textual aplicado à listagem de locais");
        }

        if (!string.IsNullOrWhiteSpace(request.AddressState))
        {
            var state = request.AddressState.Trim().ToUpperInvariant();
            query = query.Where(l => l.AddressState == state);
            logger.LogDebug("Filtro de UF aplicado à listagem de locais");
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(l => l.IsActive == request.IsActive.Value);
            logger.LogDebug("Filtro de ativo={Active} aplicado à listagem de locais", request.IsActive);
        }

        var descending = request.Direction == SortDirection.Desc;
        logger.LogDebug("Ordenando locais por {SortField} em direção {SortDirection}", request.SortBy ?? "venueName", request.Direction);
        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "enderecocidade" => descending ? query.OrderByDescending(x => x.AddressCity).ThenByDescending(x => x.Id) : query.OrderBy(x => x.AddressCity).ThenBy(x => x.Id),
            "salasquantidade" => descending ? query.OrderByDescending(x => x.Rooms.Count).ThenByDescending(x => x.Id) : query.OrderBy(x => x.Rooms.Count).ThenBy(x => x.Id),
            "localcapacidadetotal" => descending ? query.OrderByDescending(x => x.Rooms.Sum(s => s.RoomCapacity)).ThenByDescending(x => x.Id) : query.OrderBy(x => x.Rooms.Sum(s => s.RoomCapacity)).ThenBy(x => x.Id),
            _ => descending ? query.OrderByDescending(x => x.VenueName).ThenByDescending(x => x.Id) : query.OrderBy(x => x.VenueName).ThenBy(x => x.Id)
        };
        var page = await ordered
            .Select(l => new ListVenuesItemResponse(l.Id, l.VenueName, l.AddressCity, l.AddressState, l.Rooms.Count, l.Rooms.Sum(s => s.RoomCapacity), l.IsActive))
            .ToPagedResultAsync(new PagedRequest(request.Page, request.PageSize), cancellationToken);

        logger.LogInformation("Listagem de locais retornou {ReturnedCount} de {TotalCount} registro(s)", page.Items.Count, page.Total);

        return page;
    }
}
