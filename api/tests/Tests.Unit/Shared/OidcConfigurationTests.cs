using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

    private static HostApplicationBuilder Builder(string environment)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration["Oidc:Authority"] = "https://id.example.test/realms/test";
        builder.Configuration["Oidc:Audience"] = "test-api";
        return builder;
    }
}
