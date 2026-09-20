using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Audit.Shared;
using Shared.Contracts.Common;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Audit.UseCases.ListAuditRecords;

internal sealed class ListAuditRecordsUseCase(AuditDbContext db, ILogger<ListAuditRecordsUseCase> logger) : IUseCase<ListAuditRecordsRequest, PagedResult<ListAuditRecordsItemResponse>>
{
    public async Task<Result<PagedResult<ListAuditRecordsItemResponse>>> HandleAsync(ListAuditRecordsRequest request, CancellationToken cancellationToken)
    {
        var query = db.AuditRecords.TagWith("Audit.ListAuditRecords").AsNoTracking();
        logger.LogDebug("Montando consulta de auditoria: módulo={HasModule}, entidade={HasEntity}, entidadeId={HasEntityId}, usuário={HasUser}, operação={HasOperation}, início={HasStart}, fim={HasEnd}",
            !string.IsNullOrWhiteSpace(request.Module), !string.IsNullOrWhiteSpace(request.EntityName), !string.IsNullOrWhiteSpace(request.EntityId), request.UserId.HasValue,
            !string.IsNullOrWhiteSpace(request.Operation), request.OccurredFrom.HasValue, request.OccurredUntil.HasValue);

        if (!string.IsNullOrWhiteSpace(request.Module))
        {
            var module = request.Module.Trim();
            query = query.Where(r => r.Module == module);
            logger.LogDebug("Filtro de módulo aplicado à consulta de auditoria");
        }

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            var entityName = request.EntityName.Trim();
            query = query.Where(r => r.EntityName == entityName);
            logger.LogDebug("Filtro de tipo de entidade aplicado à consulta de auditoria");
        }

        if (!string.IsNullOrWhiteSpace(request.EntityId))
        {
            var entityId = request.EntityId.Trim();
            query = query.Where(r => r.EntityId == entityId);
            logger.LogDebug("Filtro de identificador de entidade aplicado à consulta de auditoria");
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(r => r.UserId == request.UserId.Value);
            logger.LogDebug("Filtro de usuário {UserId} aplicado à consulta de auditoria", request.UserId);
        }

        if (!string.IsNullOrWhiteSpace(request.Operation))
        {
            var operation = request.Operation.Trim();
            query = query.Where(r => EF.Functions.ILike(r.Operation, operation));
            logger.LogDebug("Filtro de operação aplicado à consulta de auditoria");
        }

        if (request.OccurredFrom.HasValue)
        {
            query = query.Where(r => r.OccurredOn >= request.OccurredFrom.Value);
            logger.LogDebug("Limite inicial aplicado à consulta de auditoria");
        }

        if (request.OccurredUntil.HasValue)
        {
            query = query.Where(r => r.OccurredOn <= request.OccurredUntil.Value);
            logger.LogDebug("Limite final aplicado à consulta de auditoria");
        }

        var descending = request.Direction == SortDirection.Desc;
        logger.LogDebug("Ordenando auditoria por {SortField} em direção {SortDirection}", request.SortBy ?? "occurredOn", request.Direction);
        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "module" => descending ? query.OrderByDescending(x => x.Module).ThenByDescending(x => x.Id) : query.OrderBy(x => x.Module).ThenBy(x => x.Id),
            "entidadenome" => descending ? query.OrderByDescending(x => x.EntityName).ThenByDescending(x => x.Id) : query.OrderBy(x => x.EntityName).ThenBy(x => x.Id),
            "operation" => descending ? query.OrderByDescending(x => x.Operation).ThenByDescending(x => x.Id) : query.OrderBy(x => x.Operation).ThenBy(x => x.Id),
            "usuarionome" => descending ? query.OrderByDescending(x => x.UserName).ThenByDescending(x => x.Id) : query.OrderBy(x => x.UserName).ThenBy(x => x.Id),
            _ => descending ? query.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.Id) : query.OrderBy(x => x.OccurredOn).ThenBy(x => x.Id)
        };
        var page = await ordered
            .Select(r => new ListAuditRecordsItemResponse(r.Id, r.Module, r.EntityName, r.EntityId, r.Operation, r.UserName, r.TraceId, r.OccurredOn))
            .ToPagedResultAsync(new PagedRequest(request.Page, request.PageSize), cancellationToken);

        logger.LogInformation("Consulta de auditoria retornou {ReturnedCount} de {TotalCount} registro(s)", page.Items.Count, page.Total);

        return page;
    }
}
