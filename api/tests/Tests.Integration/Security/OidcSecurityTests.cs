using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Module.Identity.Shared;
using Shared.Contracts.Identity;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;
namespace Tests.Integration.Security;
[Collection(ApiCollection.Name)]
public sealed class OidcSecurityTests(ApiFactory factory)
{
    private HttpClient Client(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
    [Fact]
    public async Task Subject_opaque_resolve_id_internal_without_trust_in_claims_reserved()
    {
        using var client = Client(TestToken.Generate(role: "Participant", additional:
            [new("app_user_id", Guid.NewGuid().ToString()), new("app_role", "Administrator")]));
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/identity/users/me");
        me.GetProperty("id").GetGuid().ShouldBe(factory.DefaultAccountId);
        me.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ShouldBe(["Participant"]);
        (await client.GetAsync("/api/v1/identity/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("long-lived")]
    [InlineData("id-token")]
    [InlineData("unprovisioned")]
    [InlineData("signature")]
    public async Task Tokens_invalid_are_rejected(string kind)
    {
        using var attacker = RSA.Create(2048);
        var token = kind switch
        {
            "issuer" => TestToken.Generate(issuer: "https://attacker.test"),
            "audience" => TestToken.Generate(audience: "another-api"),
            "expired" => TestToken.Generate(issuedAt: DateTimeOffset.UtcNow.AddMinutes(-20)),
            "long-lived" => TestToken.Generate(lifetimeSeconds: 3600),
            "id-token" => TestToken.Generate(type: "ID"),
            "unprovisioned" => TestToken.Generate(subject: "missing|" + Guid.NewGuid()),
            "signature" => TestToken.Generate(key: new RsaSecurityKey(attacker) { KeyId = TestIdentityProvider.Key.KeyId }),
            _ => throw new InvalidOperationException()
        };
        using var client = Client(token);
        (await client.GetAsync("/api/v1/identity/users/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task Role_of_another_client_or_of_realm_not_authorizes_the_API()
    {
        using var client = Client(TestToken.Generate(role: "unknown", additional:
            [new("realm_access", "{\"roles\":[\"Administrator\"]}", "JSON"), new(ClaimTypes.Role, "Administrator")]));
        (await client.GetAsync("/api/v1/operations/outbox")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Local_deactivation_and_revocation_take_effect_on_next_request(bool disable)
    {
        var subject = "security|" + Guid.NewGuid();
        var id = await factory.CreateIdentityAsync(subject);
        using var client = Client(TestToken.Generate(subject: subject));
        (await client.GetAsync("/api/v1/identity/users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await factory.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == id);
            user.UpdateAccess(!disable, DateTimeOffset.UtcNow.AddSeconds(1));
            return await db.SaveChangesAsync();
        });
        (await client.GetAsync("/api/v1/identity/users/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task Bootstrap_not_recreates_nor_elevates_users()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => factory.WithServiceAsync(sp =>
            sp.GetRequiredService<IIdentityBootstrapper>().ProvisionFirstAsync("new", "Admin", "admin@example.test", default)));
    }
    [Fact]
    public async Task Rate_limiter_partitions_by_internal_id_despite_forged_ip_headers()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:PermitLimit", "2"));
        using var client = limited.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestToken.Generate());
        for (var i = 0; i < 2; i++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"198.51.100.{i+1}");
            (await client.GetAsync("/api/v1/identity/users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.99");
        (await client.GetAsync("/api/v1/identity/users/me")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
    [Fact]
    public async Task Production_does_not_publish_documentation_or_allow_arbitrary_cors()
    {
        await using var production = factory.WithWebHostBuilder(b => b.UseEnvironment("Production").UseSetting("Database:MigrateOnStartup", "false"));
        using var client = production.CreateClient();
        (await client.GetAsync("/swagger/index.html")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/scalar")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/scalar/v1")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/scalar/scalar.js")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/scalar/scalar.aspnetcore.js")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/scalar/favicon.svg")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/openapi/v1.json")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/openapi/v1.yaml")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.test");
        var response = await client.GetAsync("/health/live");
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
    }
}
