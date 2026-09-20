using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Shared.Data;
using Shared.Data.Migrations;

namespace Module.Talks.Shared;

/// <summary>Contexto do módulo Palestras. Schema "Palestras"; tabelas Palestras, PalestraPalestrantes, PalestraConteudos, Presencas, Certificados, OutboxMessages.</summary>
public sealed class TalksDbContext(DbContextOptions<TalksDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "Talks";
    public override string Schema => SchemaName;

    public DbSet<Talk> Talks => Set<Talk>();
    public DbSet<TalkSpeaker> TalkSpeakers => Set<TalkSpeaker>();
    public DbSet<TalkContent> TalkContents => Set<TalkContent>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Certificate> Certificates => Set<Certificate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TalksDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Usado apenas por <c>dotnet ef</c> em tempo de design.</summary>
public sealed class TalksDbContextFactory : DesignTimeDbContextFactoryBase<TalksDbContext>
{
    protected override string Schema => TalksDbContext.SchemaName;
    protected override TalksDbContext Create(DbContextOptions<TalksDbContext> options) => new(options);
}
