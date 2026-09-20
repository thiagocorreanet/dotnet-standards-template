using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.People.Shared;
using Shared.Contracts.Common;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.People.UseCases.ListPeople;

internal sealed class ListPeopleUseCase(PeopleDbContext db, ILogger<ListPeopleUseCase> logger) : IUseCase<ListPeopleRequest, PagedResult<ListPeopleItemResponse>>
{
    public async Task<Result<PagedResult<ListPeopleItemResponse>>> HandleAsync(ListPeopleRequest request, CancellationToken cancellationToken)
    {
        var query = db.People.TagWith("People.ListPeople").AsNoTracking();
        logger.LogDebug("Montando listagem de pessoas: busca={HasSearch}, ativo={HasActiveFilter}", !string.IsNullOrWhiteSpace(request.Search), request.IsActive.HasValue);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.PersonName, search) ||
                EF.Functions.ILike(p.PersonEmail, search) ||
                (p.PersonCompany != null && EF.Functions.ILike(p.PersonCompany, search)));
            logger.LogDebug("Filtro textual aplicado à listagem de pessoas sem registrar seu conteúdo");
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
            logger.LogDebug("Filtro de ativo={Active} aplicado à listagem de pessoas", request.IsActive);
        }

        var descending = request.Direction == SortDirection.Desc;
        logger.LogDebug("Ordenando pessoas por {SortField} em direção {SortDirection}", request.SortBy ?? "personName", request.Direction);
        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "pessoaemail" => descending ? query.OrderByDescending(x => x.PersonEmail).ThenByDescending(x => x.Id) : query.OrderBy(x => x.PersonEmail).ThenBy(x => x.Id),
            "pessoaempresa" => descending ? query.OrderByDescending(x => x.PersonCompany).ThenByDescending(x => x.Id) : query.OrderBy(x => x.PersonCompany).ThenBy(x => x.Id),
            "estaativo" => descending ? query.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Id) : query.OrderBy(x => x.IsActive).ThenBy(x => x.Id),
            _ => descending ? query.OrderByDescending(x => x.PersonName).ThenByDescending(x => x.Id) : query.OrderBy(x => x.PersonName).ThenBy(x => x.Id)
        };
        var page = await ordered
            .Select(p => new ListPeopleItemResponse(p.Id, p.PersonName, p.PersonEmail, p.PersonCompany, p.PersonJobTitle, p.IsActive))
            .ToPagedResultAsync(new PagedRequest(request.Page, request.PageSize), cancellationToken);

        logger.LogInformation("Listagem de pessoas retornou {ReturnedCount} de {TotalCount} registro(s)", page.Items.Count, page.Total);

        return page;
    }
}
