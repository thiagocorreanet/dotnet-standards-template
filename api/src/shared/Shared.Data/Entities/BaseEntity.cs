using Shared.Contracts.Integration;

namespace Shared.Data.Entities;

/// <summary>
/// Base de todas as entidades principais: PK Guid v7 (ordenável, amigável a índices B-tree),
/// campos de auditoria, soft delete e emissão de eventos de integração.
/// </summary>
public abstract class BaseEntity : IAuditableEntity, IEventEmitter
{
    private readonly List<IIntegrationEvent> _events = [];

    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public IReadOnlyCollection<IIntegrationEvent> Events => _events.AsReadOnly();

    protected void RecordEvent(IIntegrationEvent integrationEvent) => _events.Add(integrationEvent);

    public void ClearEvents() => _events.Clear();

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
