using Microsoft.EntityFrameworkCore;
using Module.People.Domain;
using Shared.Data;
using Shared.Data.Migrations;

namespace Module.People.Shared;

/// <summary>Contexto do módulo Pessoas. Schema "Pessoas"; tabelas Pessoas e OutboxMessages.</summary>
public sealed class PeopleDbContext(DbContextOptions<PeopleDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "People";
    public override string Schema => SchemaName;

    public DbSet<Person> People => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PeopleDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado apenas por <c>dotnet ef</c> em tempo de design.</summary>
public sealed class PeopleDbContextFactory : DesignTimeDbContextFactoryBase<PeopleDbContext>
{
    protected override string Schema => PeopleDbContext.SchemaName;
    protected override PeopleDbContext Create(DbContextOptions<PeopleDbContext> options) => new(options);
}
