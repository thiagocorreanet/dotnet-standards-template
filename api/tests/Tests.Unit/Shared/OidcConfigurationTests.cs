using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Shared.Contracts.Identity;
using Shared.WebHost.Security;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class OidcConfigurationTests
{
    [Theory]
    [InlineData("Oidc:Authority", "")]
    [InlineData("Oidc:Authority", "not-a-uri")]
    [InlineData("Oidc:Authority", "http://id.example.test/realms/test")]
    [InlineData("Oidc:Authority", "ftp://id.example.test/realms/test")]
    [InlineData("Oidc:Authority", "https://id.example.test/realms/test/")]
    [InlineData("Oidc:Audience", " ")]
    [InlineData("Oidc:RequireHttpsMetadata", "false")]
    [InlineData("Oidc:MaxAccessTokenLifetimeSeconds", "59")]
    [InlineData("Oidc:MaxAccessTokenLifetimeSeconds", "601")]
    [InlineData("Oidc:AllowedRoles:0", "Root")]
    [InlineData("Oidc:RoleMap:admin", "Root")]
    [InlineData("Oidc:RoleClaimPath", "app_role")]
    [InlineData("Oidc:RoleClaimPath", "resource_access..roles")]
    [InlineData("Oidc:TokenType", " ")]
    public void Insecure_or_invalid_production_configuration_fails_at_registration(string key, string value)
    {
        var builder = Builder("Production");
        builder.Configuration[key] = value;
        Should.Throw<InvalidOperationException>(() => builder.AddOidcAuthentication());
    }

    [Theory]
    [InlineData("Production", "https", true, 60)]
    [InlineData("Production", "https", true, 600)]
    [InlineData("Development", "http", false, 300)]
    public void Supported_configuration_keeps_strict_token_validation(string environment, string scheme, bool https, int maxLifetime)
    {
        var builder = Builder(environment);
        builder.Configuration["Oidc:Authority"] = scheme + "://id.example.test/realms/test";
        builder.Configuration["Oidc:RequireHttpsMetadata"] = https.ToString();
        builder.Configuration["Oidc:MaxAccessTokenLifetimeSeconds"] = maxLifetime.ToString(System.Globalization.CultureInfo.InvariantCulture);
        builder.AddOidcAuthentication();
        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        options.RequireHttpsMetadata.ShouldBe(https);
        options.SaveToken.ShouldBeFalse();
        options.IncludeErrorDetails.ShouldBeFalse();
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        options.TokenValidationParameters.RequireSignedTokens.ShouldBeTrue();
        options.TokenValidationParameters.RequireExpirationTime.ShouldBeTrue();
        options.TokenValidationParameters.ClockSkew.ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Configured_allowed_roles_replace_the_default_list()
    {
        var builder = Builder("Production");
        builder.Configuration["Oidc:AllowedRoles:0"] = DefaultRoles.Administrator;
        builder.AddOidcAuthentication();
        using var host = builder.Build();

        host.Services.GetRequiredService<IOptions<OidcOptions>>().Value.AllowedRoles.ShouldBe([DefaultRoles.Administrator]);
    }

    [Fact]
    public void Missing_allowed_roles_use_the_default_list()
    {
        var builder = Builder("Production");
        builder.AddOidcAuthentication();
        using var host = builder.Build();

        host.Services.GetRequiredService<IOptions<OidcOptions>>().Value.AllowedRoles.ShouldBe(DefaultRoles.All);
    }

    [Fact]
    public async Task Token_type_is_required_by_default()
    {
        var (options, services) = Configure("Production");

        (await Validate(options, services, Claims(type: "ID"))).ShouldBeNull();
        (await Validate(options, services, Claims(type: null))).ShouldBeNull();
        (await Validate(options, services, Claims(type: "Bearer"))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Token_type_check_can_be_disabled_for_identity_providers_without_typ()
    {
        var (options, services) = Configure("Production", ("Oidc:RequireTokenType", "false"));

        (await Validate(options, services, Claims(type: null))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Configured_role_path_and_map_produce_internal_roles_and_drop_forged_claims()
    {
        var (options, services) = Configure("Production",
            ("Oidc:RoleClaimPath", "realm_access.roles"),
            ("Oidc:RoleMap:event-admin", DefaultRoles.Administrator));
        var claims = Claims(type: "Bearer");
        claims.Add(new("realm_access", """{"roles":["event-admin","offline_access"]}""", JsonClaimValueTypes.Json));
        claims.Add(new(OidcOptions.RoleClaim, DefaultRoles.Organizer));
        claims.Add(new(OidcOptions.UserIdClaim, Guid.NewGuid().ToString()));

        var principal = await Validate(options, services, claims);

        principal.ShouldNotBeNull();
        principal.FindAll(OidcOptions.RoleClaim).Select(c => c.Value).ShouldBe([DefaultRoles.Administrator]);
        principal.FindAll(OidcOptions.UserIdClaim).Select(c => c.Value).ShouldBe([AccountId.ToString()]);
    }

    [Fact]
    public async Task Malformed_role_claim_rejects_the_token()
    {
        var (options, services) = Configure("Production");
        var claims = Claims(type: "Bearer");
        claims.Add(new("resource_access", "[]", JsonClaimValueTypes.JsonArray));

        (await Validate(options, services, claims)).ShouldBeNull();
    }

    private static readonly Guid AccountId = Guid.NewGuid();

    private static (JwtBearerOptions Options, IServiceProvider Services) Configure(string environment, params (string Key, string Value)[] settings)
    {
        var builder = Builder(environment);
        foreach (var (key, value) in settings) builder.Configuration[key] = value;
        builder.AddOidcAuthentication();
        var resolver = Substitute.For<IIdentityResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdentityAccount(AccountId, true, DateTimeOffset.UnixEpoch));
        builder.Services.AddSingleton(resolver);
        var host = builder.Build();
        return (host.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme), host.Services);
    }

    /// <summary>Executa a validação pós-assinatura do JwtBearer. Retorna o principal aceito ou <c>null</c> se recusado.</summary>
    private static async Task<ClaimsPrincipal?> Validate(JwtBearerOptions options, IServiceProvider services, List<Claim> claims)
    {
        var context = new TokenValidatedContext(new DefaultHttpContext { RequestServices = services },
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)), options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
        };
        await options.Events.OnTokenValidated(context);
        return context.Result?.Failure is null ? context.Principal : null;
    }

    private static List<Claim> Claims(string? type)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new List<Claim>
        {
            new("sub", "external-subject"),
            new("iat", now.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("exp", (now + 300).ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        if (type is not null) claims.Add(new("typ", type));
        return claims;
    }

    private static HostApplicationBuilder Builder(string environment)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration["Oidc:Authority"] = "https://id.example.test/realms/test";
        builder.Configuration["Oidc:Audience"] = "test-api";
        return builder;
    }
}
