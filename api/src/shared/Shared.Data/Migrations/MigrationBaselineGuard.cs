using Npgsql;

namespace Shared.Data.Migrations;

/// <summary>
/// Impede que uma base incompatível seja tratada como vazia após a renomeação de schemas.
/// A conexão deve estar aberta e o migrador deve manter o advisory lock durante a verificação.
/// Este template utiliza um banco dedicado; históricos de módulos não registrados exigem revisão explícita.
/// </summary>
public static class MigrationBaselineGuard
{
    public static async Task ValidateAsync(NpgsqlConnection connection, ModuleDbContextRegistry registry, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT DISTINCT n.nspname
            FROM pg_catalog.pg_class AS c
            JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
            WHERE c.relname = '__EFMigrationsHistory'
              AND c.relkind IN ('r', 'p')
              AND NOT (n.nspname = ANY(@schemas))
            ORDER BY n.nspname
            """, connection);
        command.Parameters.AddWithValue("schemas", registry.Contexts.Select(context => context.Schema).ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Banco incompatível: existe histórico de migrações em schema não registrado. " +
                "Esta versão do template requer uma base compatível com os nomes em inglês. " +
                "Utilize um banco novo ou execute um plano explícito de migração de dados; nenhum schema foi alterado pelo migrador.");
        }
    }
}
