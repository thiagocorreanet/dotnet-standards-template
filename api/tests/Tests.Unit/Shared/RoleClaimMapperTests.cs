using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Shared.Contracts.Identity;
using Shared.WebHost.Security;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class RoleClaimMapperTests
{
    private const string Audience = "modular-api";

    [Fact]
    public void Keycloak_client_roles_are_the_default()
    {
        var principal = Principal(Json("resource_access", new Dictionary<string, object>
        {
            [Audience] = new { roles = new[] { DefaultRoles.Organizer, "uma_authorization" } },
            ["other-client"] = new { roles = new[] { DefaultRoles.Administrator } },
        }));

        Map(new OidcOptions(), principal).ShouldBe([DefaultRoles.Organizer]);
    }

    [Fact]
    public void Audience_with_dots_stays_a_single_path_segment()
    {
        const string audience = "api.example.test";
        var principal = Principal(Json("resource_access", new Dictionary<string, object>
        {
            [audience] = new { roles = new[] { DefaultRoles.Participant } },
        }));

        Map(new OidcOptions { Audience = audience }, principal).ShouldBe([DefaultRoles.Participant]);
    }

    [Fact]
    public void Keycloak_realm_roles_are_read_from_a_nested_path()
    {
        var principal = Principal(
            Json("realm_access", new { roles = new[] { DefaultRoles.Administrator, "offline_access" } }),
            Json("resource_access", new Dictionary<string, object> { [Audience] = new { roles = new[] { DefaultRoles.Participant } } }));

        Map(new OidcOptions { RoleClaimPath = "realm_access.roles" }, principal).ShouldBe([DefaultRoles.Administrator]);
    }

    [Fact]
    public void Flat_array_claim_in_entra_style_is_read_from_repeated_claims()
    {
        var principal = Principal(new Claim("roles", DefaultRoles.Organizer), new Claim("roles", "Unknown"), new Claim("roles", DefaultRoles.Organizer));

        Map(new OidcOptions { RoleClaimPath = "roles" }, principal).ShouldBe([DefaultRoles.Organizer]);
    }

    [Fact]
    public void Flat_array_claim_serialized_as_json_array_is_read()
    {
        var principal = Principal(new Claim("roles", JsonSerializer.Serialize(new[] { DefaultRoles.Participant }), JsonClaimValueTypes.JsonArray));

        Map(new OidcOptions { RoleClaimPath = "roles" }, principal).ShouldBe([DefaultRoles.Participant]);
    }

    [Fact]
    public void Role_map_translates_external_values_before_the_allowlist()
    {
        var adminGroup = Guid.NewGuid().ToString();
        var options = new OidcOptions
        {
            RoleClaimPath = "groups",
            RoleMap = new(StringComparer.Ordinal) { [adminGroup] = DefaultRoles.Administrator, ["event-staff"] = DefaultRoles.Organizer },
            AllowedRoles = [DefaultRoles.Administrator, DefaultRoles.Participant],
        };
        var principal = Principal(new Claim("groups", adminGroup), new Claim("groups", "event-staff"), new Claim("groups", Guid.NewGuid().ToString()));

        Map(options, principal).ShouldBe([DefaultRoles.Administrator]);
    }

    [Fact]
    public void Role_map_is_case_sensitive()
    {
        var options = new OidcOptions { RoleClaimPath = "roles", RoleMap = new(StringComparer.Ordinal) { ["admin"] = DefaultRoles.Administrator } };

        Map(options, Principal(new Claim("roles", "Admin"))).ShouldBeEmpty();
    }

    [Fact]
    public void Missing_claim_or_path_yields_no_roles_without_rejecting_the_token()
    {
        Map(new OidcOptions(), Principal()).ShouldBeEmpty();
        Map(new OidcOptions(), Principal(Json("resource_access", new { other = new { roles = new[] { DefaultRoles.Administrator } } }))).ShouldBeEmpty();
        Map(new OidcOptions(), Principal(Json("resource_access", new Dictionary<string, object> { [Audience] = "not-an-object" }))).ShouldBeEmpty();
        Map(new OidcOptions(), Principal(Json("resource_access", new Dictionary<string, object> { [Audience] = new { roles = "Administrator" } }))).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[\"Administrator\"]")]
    [InlineData("\"Administrator\"")]
    public void Malformed_nested_claim_rejects_the_token(string raw)
    {
        var mapper = new RoleClaimMapper(new OidcOptions { Audience = Audience }.Normalize());

        mapper.TryMap(Principal(new Claim("resource_access", raw)), out var roles).ShouldBeFalse();
        roles.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("resource_access..roles")]
    [InlineData("{audience}.roles")]
    [InlineData("app_role")]
    [InlineData("app_user_id.roles")]
    [InlineData("a.b.c.d.e.f.g")]
    public void Invalid_claim_path_fails_at_construction(string path) =>
        Should.Throw<InvalidOperationException>(() => new RoleClaimMapper(new OidcOptions { Audience = Audience, RoleClaimPath = path }.Normalize()));

    [Fact]
    public void Unknown_internal_roles_fail_at_construction()
    {
        Should.Throw<InvalidOperationException>(() => new RoleClaimMapper(new OidcOptions { Audience = Audience, AllowedRoles = ["Root"] }.Normalize()));
        Should.Throw<InvalidOperationException>(() => new RoleClaimMapper(new OidcOptions
        {
            Audience = Audience,
            RoleMap = new(StringComparer.Ordinal) { ["admin"] = "Root" },
        }.Normalize()));
    }

    private static IReadOnlyList<string> Map(OidcOptions options, ClaimsPrincipal principal)
    {
        if (string.IsNullOrEmpty(options.Audience)) options.Audience = Audience;
        new RoleClaimMapper(options.Normalize()).TryMap(principal, out var roles).ShouldBeTrue();
        return roles;
    }

    private static Claim Json(string type, object value) => new(type, JsonSerializer.Serialize(value), JsonClaimValueTypes.Json);

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));
}
