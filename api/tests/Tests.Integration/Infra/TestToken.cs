using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Shared.Contracts.Identity;
namespace Tests.Integration.Infra;
public static class TestToken
{
    public static string Generate(string role = DefaultRoles.Administrator, string name = "Usuário de Teste", Guid? id = null,
        string? subject = null, string? issuer = null, string? audience = null, int lifetimeSeconds = 300,
        string type = "Bearer", IEnumerable<Claim>? additional = null, SecurityKey? key = null, DateTimeOffset? issuedAt = null)
    {
        var now = issuedAt ?? DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", subject ?? id?.ToString() ?? TestIdentityProvider.DefaultSubject),
            new("name", name), new("preferred_username", "test-user"),
            new("iat", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("typ", type),
            new("resource_access", JsonSerializer.Serialize(new Dictionary<string, object> { [TestIdentityProvider.Audience] = new { roles = new[] { role } } }), JsonClaimValueTypes.Json)
        };
        if (additional is not null) claims.AddRange(additional);
        var token = new JwtSecurityToken(issuer ?? TestIdentityProvider.Issuer, audience ?? TestIdentityProvider.Audience,
            claims, expires: now.AddSeconds(lifetimeSeconds).UtcDateTime,
            signingCredentials: new SigningCredentials(key ?? TestIdentityProvider.Key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public static HttpClient AuthenticatedClient(this ApiFactory factory, string role = DefaultRoles.Administrator, string name = "Usuário de Teste")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Generate(role, name));
        return client;
    }
}
