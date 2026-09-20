using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.Contracts.Audit;
using Shared.Contracts.Common;
using Shared.Contracts.Integration;
using Shared.Data.Entities;
using Shared.Data.Outbox;
using Shared.Data.Transactions;

namespace Shared.Data.Interceptors;

/// <summary>
/// Um único ponto que garante três regras transversais do projeto, na MESMA transação do negócio:
/// 1) preenchimento dos campos de auditoria (CreatedAt/CreatedBy, UpdatedAt/UpdatedBy);
/// 2) soft delete (Delete vira Update com DeletedAt/DeletedBy e IsActive=false);
/// 3) Outbox: eventos de integração das entidades + um <see cref="EntityChanged"/> por alteração (trilha de auditoria).
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public const string SystemUser = "system";

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is IModuleDbContext context)
        {
            Process(eventData.Context, context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is IModuleDbContext context)
        {
            Process(eventData.Context, context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Process(DbContext db, IModuleDbContext context)
    {
        var now = timeProvider.GetUtcNow();
        var userName = currentUser.Id?.ToString() ?? SystemUser;
        var module = ModuleDbContext.ResolveModuleName(db.GetType());
        var traceId = Activity.Current?.TraceId.ToString();
        var traceParent = Activity.Current?.Id;
        var messages = new List<OutboxMessage>();

        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries().Where(e => e.Entity is not OutboxMessage and not CommandReceipt and not OutboxReplayAudit).ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is IAuditableEntity auditable)
            {
                ApplyAudit(entry, auditable, now, userName);
            }

            if (context.AuditChangesEnabled && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var record = CreateAuditRecord(entry, module, traceId);
                if (record is not null)
                {
                    messages.Add(ToOutbox(record, now, traceParent));
                }
            }
        }

        foreach (var emitter in db.ChangeTracker.Entries<IEventEmitter>().Select(e => e.Entity).Where(e => e.Events.Count > 0).ToList())
        {
            messages.AddRange(emitter.Events.Select(ev => ToOutbox(ev, now, traceParent)));
            emitter.ClearEvents();
        }

        if (messages.Count > 0)
        {
            context.OutboxMessages.AddRange(messages);
        }
    }

    private static void ApplyAudit(EntityEntry entry, IAuditableEntity entity, DateTimeOffset now, string user)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                entity.CreatedAt = now;
                entity.CreatedBy = user;
                break;
            case EntityState.Modified:
                entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                entity.UpdatedAt = now;
                entity.UpdatedBy = user;
                break;
            case EntityState.Deleted:
                entry.State = EntityState.Modified;
                entity.DeletedAt = now;
                entity.DeletedBy = user;
                entity.IsActive = false;
                break;
        }
    }

    private EntityChanged? CreateAuditRecord(EntityEntry entry, string module, string? traceId)
    {
        var key = string.Join("|", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).OrderBy(p => p.Metadata.Name)
            .Select(p => $"{p.Metadata.Name}={p.CurrentValue}"));
        var name = entry.Metadata.ClrType.Name;

        if (entry.State == EntityState.Added)
        {
            var newValues = entry.Properties.ToDictionary(p => p.Metadata.Name, p => AuditableValue(p, p.CurrentValue));
            return New(module, name, key, AuditOperations.Insert, null, newValues, traceId);
        }

        if (entry.State == EntityState.Deleted)
        {
            var previousDeleteData = entry.Properties.ToDictionary(p => p.Metadata.Name, p => AuditableValue(p, p.OriginalValue));
            return New(module, name, key, AuditOperations.Delete, previousDeleteData, [], traceId);
        }
        var modifiedEntries = entry.Properties.Where(p => p.IsModified).ToList();
        if (modifiedEntries.Count == 0)
        {
            return null;
        }

        var deleting = modifiedEntries.Any(p => p.Metadata.Name == nameof(IAuditableEntity.DeletedAt) && p.CurrentValue is not null);
        var previousValues = modifiedEntries.ToDictionary(p => p.Metadata.Name, p => AuditableValue(p, p.OriginalValue));
        var currentValues = modifiedEntries.ToDictionary(p => p.Metadata.Name, p => AuditableValue(p, p.CurrentValue));
        return New(module, name, key, deleting ? AuditOperations.Delete : AuditOperations.Update, previousValues, currentValues, traceId);
    }

    /// <summary>Propriedades marcadas com <see cref="EntityTypeBuilderExtensions.Sensitive{TProperty}"/> nunca vão para a trilha (ex.: hash de senha).</summary>
    private static object? AuditableValue(PropertyEntry property, object? value) =>
        value is null ? null : property.Metadata.FindAnnotation(EntityTypeBuilderExtensions.AuditValueAnnotation)?.Value is true
            && property.Metadata.FindAnnotation(EntityTypeBuilderExtensions.SensitiveAnnotation)?.Value is not true ? value : "***";

    private EntityChanged New(string module, string entity, string key, string operation, Dictionary<string, object?>? before, Dictionary<string, object?> after, string? traceId) =>
        new(module, entity, key, operation,
            before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            JsonSerializer.Serialize(after, JsonOptions),
            currentUser.Id, currentUser.Id?.ToString() ?? SystemUser, traceId);

    private static OutboxMessage ToOutbox(IIntegrationEvent integrationEvent, DateTimeOffset now, string? traceParent)
    {
        var type = integrationEvent.GetType();
        return new OutboxMessage
        {
            Type = EventContractAttribute.For(type).Name,
            Payload = JsonSerializer.Serialize(integrationEvent, type, JsonOptions),
            OccurredOn = now,
            TraceParent = traceParent,
        };
    }
}
