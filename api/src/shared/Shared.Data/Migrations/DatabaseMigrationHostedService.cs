using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Shared.Data.Migrations;

/// <summary>
/// Aplica as migrações de todos os módulos na subida (quando <c>Database:MigrateOnStartup=true</c>).
/// Usa advisory lock do PostgreSQL para que várias instâncias subindo em paralelo não migrem ao mesmo tempo.
/// Em produção com alta criticidade, prefira executar as migrações em pipeline (ver runbook).
/// </summary>
public sealed class DatabaseMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    ModuleDbContextRegistry registry,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    private const long LockKey = 7_202_410; // "modular-api" migrações

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Database:MigrateOnStartup", false))
        {
            logger.LogInformation("Migração automática desabilitada (Database:MigrateOnStartup=false)");
            foreach (var (contextType, schema) in registry.Contexts)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                    throw new InvalidOperationException($"Schema {schema} desatualizado. Execute o job migrate.");
            }
            return;
        }

        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Migração automática proibida fora de Development. Execute o comando migrate com identidade DDL separada.");
        await RunAsync(cancellationToken);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("ModularApi") ?? throw new InvalidOperationException("ConnectionStrings:ModularApi não configurada");
        await using var lockConnection = new NpgsqlConnection(connectionString);
        await lockConnection.OpenAsync(cancellationToken);
        await using (var cmd = new NpgsqlCommand($"SELECT pg_advisory_lock({LockKey})", lockConnection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await MigrationBaselineGuard.ValidateAsync(lockConnection, registry, cancellationToken);
            foreach (var (contextType, schema) in registry.Contexts)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                var pendingItems = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pendingItems.Count == 0)
                {
                    logger.LogInformation("Schema {Schema}: nenhuma migração pendente", schema);
                    continue;
                }

                logger.LogInformation("Schema {Schema}: aplicando {Count} migração(ões): {Migrations}", schema, pendingItems.Count, string.Join(", ", pendingItems));
                await db.Database.MigrateAsync(cancellationToken);
            }
        }
        finally
        {
            await using var cmd = new NpgsqlCommand($"SELECT pg_advisory_unlock({LockKey})", lockConnection);
            await cmd.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
