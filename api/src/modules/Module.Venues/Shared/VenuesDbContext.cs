using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Shared.Data;
using Shared.Data.Migrations;

namespace Module.Venues.Shared;

/// <summary>Contexto do módulo Locais. Schema "Locais"; tabelas Locais, Salas, OutboxMessages.</summary>
public sealed class VenuesDbContext(DbContextOptions<VenuesDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "Venues";
    public override string Schema => SchemaName;

    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VenuesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado apenas por <c>dotnet ef</c> em tempo de design.</summary>
public sealed class VenuesDbContextFactory : DesignTimeDbContextFactoryBase<VenuesDbContext>
{
    protected override string Schema => VenuesDbContext.SchemaName;
    protected override VenuesDbContext Create(DbContextOptions<VenuesDbContext> options) => new(options);
}
