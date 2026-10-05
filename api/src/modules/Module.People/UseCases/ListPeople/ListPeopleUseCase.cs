using Microsoft.EntityFrameworkCore;
using Module.People.Shared;
using Shared.Contracts.Common;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.People.UseCases.ListPeople;

internal sealed class ListPeopleUseCase(PeopleDbContext db) : IUseCase<ListPeopleRequest, PagedResult<ListPeopleItemResponse>>
{
    public async Task<Result<PagedResult<ListPeopleItemResponse>>> HandleAsync(ListPeopleRequest request, CancellationToken cancellationToken)
    {
        var query = db.People.TagWith("People.ListPeople").AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.PersonName, search) ||
                EF.Functions.ILike(p.PersonEmail, search) ||
                (p.PersonCompany != null && EF.Functions.ILike(p.PersonCompany, search)));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        var descending = request.Direction == SortDirection.Desc;
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

        return page;
    }
}
