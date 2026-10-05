using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Common;

namespace Shared.Data.Extensions;

public static class PagingExtensions
{
    /// <summary>Pagina uma consulta já projetada (somente as colunas necessárias) e ordenada.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PagedRequest paging, CancellationToken cancellationToken)
    {
        var page = paging.NormalizedPage;
        var size = paging.NormalizedSize;
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page, size, total);
    }
}
