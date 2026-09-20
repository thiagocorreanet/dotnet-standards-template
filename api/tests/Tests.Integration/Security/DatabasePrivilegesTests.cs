using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Data.Migrations;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;
namespace Tests.Integration.Security;
[Collection(ApiCollection.Name)]
public sealed class DatabasePrivilegesTests(ApiFactory factory)
{
    [Fact]
    public async Task Runtime_can_read_write_business_but_cannot_ddl_or_mutate_audit()
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand("CREATE ROLE least_privilege_probe NOLOGIN", connection);
        await create.ExecuteNonQueryAsync();
        await factory.WithServiceAsync(async sp =>
        {
            await RuntimeDatabasePrivileges.GrantAsync(sp, sp.GetRequiredService<ModuleDbContextRegistry>(), "least_privilege_probe", default);
            return true;
        });
        await using var query = new NpgsqlCommand("""
            SELECT has_schema_privilege('least_privilege_probe', 'Identity', 'USAGE'),
                   has_schema_privilege('least_privilege_probe', 'Identity', 'CREATE'),
                   has_table_privilege('least_privilege_probe', '"Identity"."Users"', 'UPDATE'),
                   has_table_privilege('least_privilege_probe', '"Audit"."AuditRecords"', 'INSERT'),
                   has_table_privilege('least_privilege_probe', '"Audit"."AuditRecords"', 'UPDATE'),
                   has_table_privilege('least_privilege_probe', '"Audit"."AuditRecords"', 'DELETE'),
                   has_table_privilege('least_privilege_probe', '"Identity"."OutboxReplayAudit"', 'DELETE')
            """, connection);
        await using var result = await query.ExecuteReaderAsync();
        (await result.ReadAsync()).ShouldBeTrue();
        Enumerable.Range(0, 7).Select(result.GetBoolean).ShouldBe([true, false, true, true, false, false, false]);
    }
}
