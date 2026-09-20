using Shared.Contracts.Audit;

namespace Module.Audit.Domain;

/// <summary>
/// Trilha de auditoria: um registro imutável por alteração de entidade em qualquer módulo.
/// NÃO herda <c>BaseEntity</c> (não sofre update, soft delete nem auto-auditoria). O <see cref="Id"/> é o Id do evento
/// <see cref="EntityChanged"/> que o originou, o que torna a persistência idempotente diante de reentregas do Outbox.
/// </summary>
public sealed class AuditRecord : global::Shared.Data.Entities.IImmutableRecord
{
    private AuditRecord()
    {
    }

    public Guid Id { get; private set; }
    public string Module { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;

    /// <summary>Inclusao, Alteracao ou Exclusao (ver <see cref="AuditOperations"/>).</summary>
    public string Operation { get; private set; } = string.Empty;

    /// <summary>JSON (jsonb) com os valores anteriores das propriedades alteradas; nulo em inclusões.</summary>
    public string? PreviousData { get; private set; }

    /// <summary>JSON (jsonb) com os valores novos (todas as propriedades em inclusões; só as alteradas nos demais casos).</summary>
    public string? NewData { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? TraceId { get; private set; }

    /// <summary>Momento em que a alteração aconteceu no módulo de origem.</summary>
    public DateTimeOffset OccurredOn { get; private set; }

    /// <summary>Momento em que este módulo persistiu o registro (latência do Outbox = RecordedAt - OccurredOn).</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    public static AuditRecord Create(EntityChanged eventEntity, DateTimeOffset recordedAt) => new()
    {
        Id = eventEntity.Id,
        Module = eventEntity.Module,
        EntityName = eventEntity.EntityName,
        EntityId = eventEntity.EntityId,
        Operation = eventEntity.Operation,
        PreviousData = eventEntity.PreviousData,
        NewData = eventEntity.NewData,
        UserId = eventEntity.UserId,
        UserName = eventEntity.UserName,
        TraceId = eventEntity.TraceId,
        OccurredOn = eventEntity.OccurredOn,
        RecordedAt = recordedAt,
    };
}
