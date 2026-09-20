using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Entities;
using Shared.Data.Outbox;
using Shared.Data.Transactions;

namespace Shared.Data;

/// <summary>
/// Convenções de modelo compartilhadas por todo DbContext de módulo (Outbox, soft delete, tipos padrão).
/// <see cref="ModuleDbContext"/> as aplica automaticamente; contextos com outra base (ex.: Identity) chamam os mesmos métodos.
/// </summary>
public static class ModuleModelConventions
{
    public const string SoftDeleteFilterName = "SoftDelete";

    /// <summary>Convenções de tipos: strings com tamanho padrão, decimais com precisão e enums como texto.</summary>
    public static void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>().HaveMaxLength(200);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);
    }

    /// <summary>Tabela <c>OutboxMessages</c> no schema do módulo, payload jsonb e índice para o processador.</summary>
    public static void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxReplayAudit>(b =>
        {
            b.ToTable("OutboxReplayAudit");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ReasonCode).HasMaxLength(80);
        });
        modelBuilder.Entity<CommandReceipt>(b =>
        {
            b.ToTable("CommandReceipts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasIndex(x => x.CommittedAt);
        });
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Type).HasMaxLength(500);
            b.Property(m => m.Payload).HasColumnType("jsonb");
            b.Property(m => m.Error).HasMaxLength(2000);
            b.Property(m => m.TraceParent).HasMaxLength(100);
            b.HasIndex(m => m.ClaimToken);
            b.HasIndex(m => new { m.ProcessedOn, m.DeadLetteredAt, m.NextAttemptAt, m.LockedUntil, m.OccurredOn }).HasDatabaseName("IX_OutboxMessages_Pending");
        });
    }

    /// <summary>
    /// Para toda entidade <see cref="IAuditableEntity"/> raiz: tamanhos dos campos de auditoria, índice em <c>DeletedAt</c>,
    /// filtro global nomeado de soft delete e exclusão da coleção de eventos do mapeamento.
    /// Chame após as entidades estarem configuradas (ex.: depois de <c>base.OnModelCreating</c>).
    /// </summary>
    public static void ConfigureAuditableEntities(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType) || entityType.BaseType is not null)
            {
                continue;
            }

            var builder = modelBuilder.Entity(entityType.ClrType);
            builder.Property(nameof(IAuditableEntity.CreatedBy)).HasMaxLength(150);
            builder.Property(nameof(IAuditableEntity.UpdatedBy)).HasMaxLength(150);
            builder.Property(nameof(IAuditableEntity.DeletedBy)).HasMaxLength(150);
            builder.HasIndex(nameof(IAuditableEntity.DeletedAt));
            builder.HasQueryFilter(SoftDeleteFilterName, BuildSoftDeleteFilter(entityType.ClrType));
            builder.Ignore(nameof(IEventEmitter.Events));
        }
    }

    private static LambdaExpression BuildSoftDeleteFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var property = Expression.Property(parameter, nameof(IAuditableEntity.DeletedAt));
        var body = Expression.Equal(property, Expression.Constant(null, typeof(DateTimeOffset?)));
        return Expression.Lambda(body, parameter);
    }
}
