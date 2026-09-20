using Shared.Contracts.Integration;

namespace Shared.Contracts.Audit;

/// <summary>
/// Emitido automaticamente pela infraestrutura de dados a cada Insert/Update/Delete(soft) de qualquer entidade auditável.
/// Consumido pelo módulo Auditoria, que persiste o registro no seu próprio schema.
/// </summary>
[EventContract("audit.entity-changed.v1", requiresConsumer: true)]
public sealed record EntityChanged(
    string Module,
    string EntityName,
    string EntityId,
    string Operation,
    string? PreviousData,
    string? NewData,
    Guid? UserId,
    string? UserName,
    string? TraceId) : IntegrationEvent;

public static class AuditOperations
{
    public const string Insert = "Insert";
    public const string Update = "Update";
    public const string Delete = "Delete";
}
