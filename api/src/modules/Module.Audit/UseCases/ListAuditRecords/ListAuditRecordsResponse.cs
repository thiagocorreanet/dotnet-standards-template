namespace Module.Audit.UseCases.ListAuditRecords;

public sealed record ListAuditRecordsItemResponse(
    Guid Id,
    string Module,
    string EntityName,
    string EntityId,
    string Operation,
    string? UserName,
    string? TraceId,
    DateTimeOffset OccurredOn);
