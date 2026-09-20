using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shared.Data.Migrations;

/// <summary>
/// Fábrica usada apenas pelo <c>dotnet ef migrations add</c>. Cada módulo declara a sua com uma linha.
/// Connection string via env <c>ConnectionStrings__ModularApi</c>; caso contrário, o padrão do docker-compose.
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext> where TContext : ModuleDbContext
{
    protected abstract string Schema { get; }
    protected abstract TContext Create(DbContextOptions<TContext> options);

    public TContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ModularApi")
            ?? "Host=localhost;Database=modular_api_design;Username=migrator";
        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema))
            .Options;
        return Create(options);
    }
}
