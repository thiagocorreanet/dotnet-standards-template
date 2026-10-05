using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Data.Migrations;

namespace Module.ModuleName.Shared;

/// <summary>
/// Contexto único do módulo ModuleName, no schema de mesmo nome. Já traz as tabelas de mensageria do módulo
/// (Outbox, Inbox, recibos de comando); mapeie as entidades em <c>Shared/Configurations/</c>.
/// </summary>
public sealed class ModuleNameDbContext(DbContextOptions<ModuleNameDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "ModuleName";
    public override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModuleNameDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado apenas por <c>dotnet ef</c> em tempo de design.</summary>
public sealed class ModuleNameDbContextFactory : DesignTimeDbContextFactoryBase<ModuleNameDbContext>
{
    protected override string Schema => ModuleNameDbContext.SchemaName;
    protected override ModuleNameDbContext Create(DbContextOptions<ModuleNameDbContext> options) => new(options);
}
