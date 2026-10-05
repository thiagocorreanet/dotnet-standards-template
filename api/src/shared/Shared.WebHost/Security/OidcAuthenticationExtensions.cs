using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Shared.Contracts.Identity;

namespace Shared.WebHost.Security;
public static class OidcAuthenticationExtensions
{
    public static void AddOidcAuthentication(this IHostApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(OidcOptions.Section);
        var settings = (section.Get<OidcOptions>() ?? new()).Normalize();
        if (!Uri.TryCreate(settings.Authority, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(builder.Environment.IsDevelopment() && uri.Scheme == "http")) ||
            settings.Authority.EndsWith('/') || string.IsNullOrWhiteSpace(settings.Audience) ||
            settings.MaxAccessTokenLifetimeSeconds is < 60 or > 600 ||
            (settings.RequireTokenType && string.IsNullOrWhiteSpace(settings.TokenType)) ||
            (!builder.Environment.IsDevelopment() && !settings.RequireHttpsMetadata))
            throw new InvalidOperationException("Configure Oidc:Authority HTTPS (sem barra final), Audience, TokenType e validade máxima entre 60 e 600 segundos.");
        var roleMapper = new RoleClaimMapper(settings);
        builder.Services.Configure<OidcOptions>(section);
        builder.Services.PostConfigure<OidcOptions>(options => options.Normalize());
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.Authority = settings.Authority;
            o.Audience = settings.Audience;
            o.RequireHttpsMetadata = settings.RequireHttpsMetadata;
            o.MapInboundClaims = false;
            o.SaveToken = false;
            o.IncludeErrorDetails = false;
            o.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = settings.Authority,
                ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                ValidateLifetime = true, RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512],
                ClockSkew = TimeSpan.FromSeconds(15), NameClaimType = "preferred_username", RoleClaimType = OidcOptions.RoleClaim
            };
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    var principal = ctx.Principal!;
                    var subject = principal.FindFirstValue("sub");
                    if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255 ||
                        (settings.RequireTokenType && principal.FindFirstValue("typ") != settings.TokenType) ||
                        !long.TryParse(principal.FindFirstValue("iat"), out var issued) ||
                        !long.TryParse(principal.FindFirstValue("exp"), out var expires) ||
                        issued < 0 || expires <= issued || expires - issued > settings.MaxAccessTokenLifetimeSeconds ||
                        issued > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 15)
                    {
                        ctx.Fail("Token inválido."); return;
                    }
                    var account = await ctx.HttpContext.RequestServices.GetRequiredService<IIdentityResolver>()
                        .ResolveAsync(settings.Authority, subject, ctx.HttpContext.RequestAborted);
                    if (account is null || !account.Enabled || DateTimeOffset.FromUnixTimeSeconds(issued) < account.TokensValidAfter)
                    {
                        ctx.Fail("Identidade indisponível."); return;
                    }
                    // Nunca aceitar claims internas fornecidas pelo token, mesmo que assinado.
                    foreach (var identity in principal.Identities)
                        foreach (var claim in identity.Claims.Where(c => c.Type is OidcOptions.RoleClaim or OidcOptions.UserIdClaim).ToArray())
                            identity.RemoveClaim(claim);
                    var target = (ClaimsIdentity)principal.Identity!;
                    target.AddClaim(new(OidcOptions.UserIdClaim, account.Id.ToString()));
                    if (!roleMapper.TryMap(principal, out var roles)) { ctx.Fail("Claims inválidas."); return; }
                    foreach (var role in roles)
                        target.AddClaim(new(OidcOptions.RoleClaim, role));
                }
            };
        });
    }
}
