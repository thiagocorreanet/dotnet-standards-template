using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
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
        var logger = query.Provider is IInfrastructure<IServiceProvider> infrastructure
            ? infrastructure.Instance.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
                ? factory.CreateLogger("Shared.Data.Paging")
                : null
            : null;
        logger?.LogInformation(
            "Consulta paginada retornou {ReturnedCount} de {TotalCount} registro(s) na página {Page} com tamanho {PageSize}",
            items.Count, total, page, size);
        return new PagedResult<T>(items, page, size, total);
    }
}
