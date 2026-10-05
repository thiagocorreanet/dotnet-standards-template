using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Audit;

/// <summary>
/// Núcleo (Identity + Audit), com PostgreSQL real: o cadastro de um vínculo gera auditoria pela Outbox, e a consulta
/// administrativa filtra e detalha esse registro. Cobre também as regras de acesso do UpdateUserAccess.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuditQueryTests(ApiFactory factory)
{
    [Fact]
    public async Task Administrator_filters_and_reads_the_audit_of_a_registered_identity()
    {
        using var client = factory.AuthenticatedClient();
        var userId = await RegisterAsync(client);
        // A chave auditada é a PK no formato <Nome>=<valor>.
        var filter = $"module=Identity&entityName=User&entityId={Uri.EscapeDataString("Id=" + userId)}";
        var record = await WaitForSingleAsync(client, filter);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"));
        var until = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"));
        (await TotalAsync(client, $"{filter}&operation=insert&userId={factory.DefaultAccountId}&occurredFrom={from}&occurredUntil={until}")).ShouldBe(1);
        (await TotalAsync(client, $"{filter}&operation=Delete")).ShouldBe(0);
        (await TotalAsync(client, $"{filter}&userId={Guid.NewGuid()}")).ShouldBe(0);
        foreach (var sort in new[] { "sortBy=module&direction=Asc", "sortBy=operation&direction=Desc", "sortBy=occurredOn&direction=Asc" })
            (await TotalAsync(client, $"{filter}&{sort}")).ShouldBe(1);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/audit/records/{record.GetProperty("id").GetGuid()}");
        detail.GetProperty("entityId").GetString().ShouldBe("Id=" + userId);
        detail.GetProperty("operation").GetString().ShouldBe("Insert");

        using var missing = await client.GetAsync($"/api/v1/audit/records/{Guid.NewGuid()}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("Audit.RecordNotFound");
    }

    [Fact]
    public async Task Access_changes_reject_self_deactivation_and_unknown_users_and_revoke_tokens()
    {
        using var client = factory.AuthenticatedClient();

        using var self = await client.PutAsJsonAsync($"/api/v1/identity/users/{factory.DefaultAccountId}/access", new { isActive = false, revokeTokens = false });
        self.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await self.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("Identity.SelfDeactivation");

        using var unknown = await client.PutAsJsonAsync($"/api/v1/identity/users/{Guid.NewGuid()}/access", new { isActive = true, revokeTokens = false });
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("Identity.NotFound");

        var userId = await RegisterAsync(client);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        using var revoked = await client.PutAsJsonAsync($"/api/v1/identity/users/{userId}/access", new { isActive = false, revokeTokens = true });
        revoked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await revoked.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        body.GetProperty("tokensValidAfter").GetDateTimeOffset().ShouldBeGreaterThan(before);
    }

    private static async Task<Guid> RegisterAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/identity/users",
            new { subject = "audit|" + Guid.NewGuid(), userName = "Pessoa auditada", userEmail = "auditada@example.test" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>A auditoria chega pela Outbox; espera o registro aparecer, com prazo.</summary>
    private static async Task<JsonElement> WaitForSingleAsync(HttpClient client, string filter)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/audit/records?{filter}", deadline.Token);
            if (page.GetProperty("total").GetInt64() == 1) return page.GetProperty("items")[0];
            await Task.Delay(200, deadline.Token);
        }
    }

    private static async Task<long> TotalAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"/api/v1/audit/records?{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt64();
    }
}
