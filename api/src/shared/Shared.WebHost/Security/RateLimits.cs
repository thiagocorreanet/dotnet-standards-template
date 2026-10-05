using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Shared.WebHost.Security;

/// <summary>
/// Partição, validação e políticas nomeadas de rate limiting. Genérico: não conhece módulo nem regra de negócio.
/// Os limites são por réplica; limite global entre réplicas é responsabilidade da borda.
/// </summary>
public static class RateLimits
{
    public const string Section = "RateLimiting";
    public const int MinPermitLimit = 1;
    public const int MaxPermitLimit = 100_000;
    public const int MinWindowSeconds = 1;
    public const int MaxWindowSeconds = 3_600;

    /// <summary>
    /// Chave de partição: <c>user:{id interno}</c> para requisição autenticada, <c>ip:{ip}</c> para anônima. O prefixo impede
    /// colisão entre os dois espaços. A chave identifica uma pessoa ou um endereço: não registre em log nem em métrica.
    /// </summary>
    public static string PartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirst(OidcOptions.UserIdClaim)?.Value;
        return userId is null ? "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown") : "user:" + userId;
    }

    /// <summary>
    /// Registra uma política nomeada de janela fixa, particionada por <see cref="PartitionKey"/>. Aplique no endpoint com
    /// <c>.RequireRateLimiting(name)</c>. Os valores do código podem ser sobrescritos por configuração em
    /// <c>RateLimiting:Policies:{name}:PermitLimit</c> e <c>WindowSeconds</c>; faixa inválida falha no startup.
    /// </summary>
    /// <remarks>A política soma-se ao limite global: a requisição precisa passar pelos dois.</remarks>
    public static IHostApplicationBuilder AddFixedWindowRateLimitPolicy(this IHostApplicationBuilder builder, string name, int permitLimit, int windowSeconds)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '.')))
            throw new InvalidOperationException($"Nome de política de rate limiting inválido: '{name}'. Use letras, dígitos, '-' ou '.'.");
        var section = builder.Configuration.GetSection($"{Section}:Policies:{name}");
        var limit = ReadPermitLimit(section, "PermitLimit", permitLimit, $"{Section}:Policies:{name}:PermitLimit");
        var window = ReadWindowSeconds(section, "WindowSeconds", windowSeconds, $"{Section}:Policies:{name}:WindowSeconds");
        builder.Services.Configure<RateLimiterOptions>(o => o.AddPolicy(name, context =>
            RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => FixedWindow(limit, window))));
        return builder;
    }

    /// <summary>Grava <c>Retry-After</c> em segundos inteiros quando o limiter informa o prazo; sem metadado, não grava.</summary>
    public static void WriteRetryAfter(HttpResponse response, RateLimitLease lease)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter = ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(NumberFormatInfo.InvariantInfo);
    }

    /// <summary>Endpoint marcado com <c>DisableRateLimiting()</c> (ex.: health checks) não conta em nenhum limiter.</summary>
    public static bool IsExempt(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null;

    internal static FixedWindowRateLimiterOptions FixedWindow(int permitLimit, int windowSeconds) =>
        new() { PermitLimit = permitLimit, Window = TimeSpan.FromSeconds(windowSeconds), QueueLimit = 0 };

    internal static int ReadPermitLimit(IConfiguration configuration, string key, int fallback, string displayKey)
    {
        var value = configuration.GetValue(key, fallback);
        return value is < MinPermitLimit or > MaxPermitLimit
            ? throw new InvalidOperationException($"{displayKey} inválido: use de {MinPermitLimit} a {MaxPermitLimit}.")
            : value;
    }

    internal static int ReadWindowSeconds(IConfiguration configuration, string key, int fallback, string displayKey)
    {
        var value = configuration.GetValue(key, fallback);
        return value is < MinWindowSeconds or > MaxWindowSeconds
            ? throw new InvalidOperationException($"{displayKey} inválido: use de {MinWindowSeconds} a {MaxWindowSeconds} segundos.")
            : value;
    }
}
