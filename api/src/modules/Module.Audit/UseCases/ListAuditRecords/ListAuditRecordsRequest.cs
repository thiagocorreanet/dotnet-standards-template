namespace Module.Audit.UseCases.ListAuditRecords;

/// <summary>Filtros de consulta da trilha de auditoria (query string). Todos opcionais; combinados com AND.</summary>
public sealed record ListAuditRecordsRequest(
    string? Module,
    string? EntityName,
    string? EntityId,
    Guid? UserId,
    string? Operation,
    DateTimeOffset? OccurredFrom,
    DateTimeOffset? OccurredUntil,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    global::Shared.Contracts.Common.SortDirection Direction = global::Shared.Contracts.Common.SortDirection.Desc);
