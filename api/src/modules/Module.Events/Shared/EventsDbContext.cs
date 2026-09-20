using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Shared.Data;
using Shared.Data.Migrations;

namespace Module.Events.Shared;

/// <summary>Contexto do módulo Eventos. Schema "Eventos"; tabelas Eventos, Inscricoes, OutboxMessages.</summary>
public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "Events";
    public override string Schema => SchemaName;

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Track> Tracks => Set<Track>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado apenas por <c>dotnet ef</c> em tempo de design.</summary>
public sealed class EventsDbContextFactory : DesignTimeDbContextFactoryBase<EventsDbContext>
{
    protected override string Schema => EventsDbContext.SchemaName;
    protected override EventsDbContext Create(DbContextOptions<EventsDbContext> options) => new(options);
}
