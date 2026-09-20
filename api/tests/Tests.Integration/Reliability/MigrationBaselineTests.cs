using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Data.Migrations;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Reliability;

public sealed class MigrationBaselineTests
{
    [Fact]
    public async Task Migrator_rejects_unknown_history_before_applying_any_registered_schema()
    {
        await using var isolated = new ApiFactory { OutboxEnabled = false };
        await isolated.StartDatabaseAsync();
        await using var connection = new NpgsqlConnection(isolated.ConnectionString);
        await connection.OpenAsync();
        await using (var seed = new NpgsqlCommand("""
            CREATE SCHEMA "LegacyProduct";
            CREATE TABLE "LegacyProduct"."__EFMigrationsHistory" ("MigrationId" text PRIMARY KEY);
            INSERT INTO "LegacyProduct"."__EFMigrationsHistory" VALUES ('preserve-existing-data');
            """, connection))
        {
            await seed.ExecuteNonQueryAsync();
        }

        // Executa o caminho real da inicialização, antes da criação dos schemas atuais.
        var error = Should.Throw<InvalidOperationException>(() => isolated.CreateClient());
        error.Message.ShouldContain("Banco incompatível");

        await using var verify = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM "LegacyProduct"."__EFMigrationsHistory"
                    WHERE "MigrationId" = 'preserve-existing-data'),
                   (SELECT count(*) FROM pg_catalog.pg_class
                    WHERE relname = '__EFMigrationsHistory')
            """, connection);
        await using var reader = await verify.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        reader.GetInt64(0).ShouldBe(1);
        reader.GetInt64(1).ShouldBe(1);
    }

    [Fact]
    public async Task Migrator_accepts_fresh_database_and_is_idempotent_with_current_schemas()
    {
        await using var isolated = new ApiFactory { OutboxEnabled = false };
        await isolated.InitializeAsync();
        await isolated.Services.GetRequiredService<DatabaseMigrationHostedService>().RunAsync(default);
        await using var connection = new NpgsqlConnection(isolated.ConnectionString);
        await connection.OpenAsync();
        await MigrationBaselineGuard.ValidateAsync(connection,
            isolated.Services.GetRequiredService<ModuleDbContextRegistry>(), default);
    }
}
