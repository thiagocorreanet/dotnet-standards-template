using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Data.Entities;
namespace Shared.Data.Migrations;
/// <summary>Concede privilégios só aos schemas/tabelas dos módulos registrados, nunca CREATE ao runtime.</summary>
public static class RuntimeDatabasePrivileges
{
    public static async Task GrantAsync(IServiceProvider services, ModuleDbContextRegistry registry, string role, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(role)) return;
        var quote = new NpgsqlCommandBuilder();
        // GRANT/REVOKE não aceitam parâmetros para schema, tabela e role: os identificadores vêm do registro
        // de módulos e da configuração, e passam por QuoteIdentifier antes de entrar no comando.
#pragma warning disable EF1002 // Interpolação inserida diretamente no SQL
        foreach (var (contextType, schema) in registry.Contexts)
        {
            await using var scope = services.CreateAsyncScope();
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            var schemaSql = quote.QuoteIdentifier(schema);
            var roleSql = quote.QuoteIdentifier(role);
            await db.Database.ExecuteSqlRawAsync($"GRANT USAGE ON SCHEMA {schemaSql} TO {roleSql}", ct);
            foreach (var entity in db.Model.GetEntityTypes().Where(e => e.GetTableName() is not null)
                .GroupBy(e => e.GetTableName()).Select(g => g.First()))
            {
                var table = schemaSql + "." + quote.QuoteIdentifier(entity.GetTableName()!);
                var immutable = typeof(IImmutableRecord).IsAssignableFrom(entity.ClrType);
                await db.Database.ExecuteSqlRawAsync($"REVOKE ALL ON TABLE {table} FROM {roleSql}", ct);
                await db.Database.ExecuteSqlRawAsync($"GRANT {(immutable ? "SELECT, INSERT" : "SELECT, INSERT, UPDATE, DELETE")} ON TABLE {table} TO {roleSql}", ct);
            }
            await db.Database.ExecuteSqlRawAsync($"GRANT SELECT ON TABLE {schemaSql}.\"__EFMigrationsHistory\" TO {roleSql}", ct);
        }
#pragma warning restore EF1002
    }
}
