namespace Module.Audit.UseCases.GetAuditRecord;

/// <summary><c>PreviousData</c> e <c>NewData</c> são strings JSON (conteúdo do jsonb), para o cliente renderizar o diff.</summary>
public sealed record GetAuditRecordResponse(
    Guid Id,
    string Module,
    string EntityName,
    string EntityId,
    string Operation,
    string? PreviousData,
    string? NewData,
    Guid? UserId,
    string? UserName,
    string? TraceId,
    DateTimeOffset OccurredOn,
    DateTimeOffset RecordedAt);
