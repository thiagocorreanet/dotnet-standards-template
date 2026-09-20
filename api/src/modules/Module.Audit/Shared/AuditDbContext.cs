using Microsoft.EntityFrameworkCore;
using Module.Audit.Domain;
using Shared.Data;
using Shared.Data.Migrations;

namespace Module.Audit.Shared;

/// <summary>
/// Contexto do módulo Auditoria. Schema "Auditoria"; tabela RegistrosAuditoria (append-only).
/// <see cref="AuditChangesEnabled"/> é falso: gravar um registro de auditoria não gera outro registro de auditoria.
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "Audit";
    public override string Schema => SchemaName;
    public override bool AuditChangesEnabled => false;

    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado apenas por <c>dotnet ef</c> em tempo de design.</summary>
public sealed class AuditDbContextFactory : DesignTimeDbContextFactoryBase<AuditDbContext>
{
    protected override string Schema => AuditDbContext.SchemaName;
    protected override AuditDbContext Create(DbContextOptions<AuditDbContext> options) => new(options);
}
