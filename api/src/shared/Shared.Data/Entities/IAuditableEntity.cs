namespace Shared.Data.Entities;

/// <summary>
/// Campos obrigatórios de auditoria e soft delete de toda entidade principal.
/// Preenchidos automaticamente pelo <c>AuditSaveChangesInterceptor</c>; nunca manualmente.
/// </summary>
public interface IAuditableEntity
{
    Guid Id { get; }
    DateTimeOffset CreatedAt { get; set; }
    string CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
    bool IsActive { get; set; }
}
