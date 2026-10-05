using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
namespace Shared.WebHost.Security;
/// <summary>
/// Limite por IP antes da validação criptográfica. O limiter global por usuário roda depois da autenticação.
/// Endpoints com <c>DisableRateLimiting()</c> (health checks) não contam.
/// </summary>
internal sealed class IngressRateLimitMiddleware : IDisposable
{
    private readonly RequestDelegate next;
    private readonly PartitionedRateLimiter<HttpContext> limiter;
    public IngressRateLimitMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        this.next = next;
        var limit = RateLimits.ReadPermitLimit(configuration, "RateLimiting:IngressPermitLimit", 600, "RateLimiting:IngressPermitLimit");
        limiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => RateLimits.FixedWindow(limit, 60)));
    }
    public async Task InvokeAsync(HttpContext context)
    {
        if (RateLimits.IsExempt(context))
        {
            await next(context);
            return;
        }
        using var lease = await limiter.AcquireAsync(context, cancellationToken: context.RequestAborted);
        if (!lease.IsAcquired)
        {
            RateLimits.WriteRetryAfter(context.Response, lease);
            await Results.Problem(statusCode: 429, title: "Muitas requisições",
                detail: "Limite de requisições excedido. Tente novamente em instantes.",
                type: "urn:problem:IngressRateLimit",
                extensions: new Dictionary<string, object?> { ["code"] = "IngressRateLimit" }).ExecuteAsync(context);
            return;
        }
        await next(context);
    }
    public void Dispose() => limiter.Dispose();
}
