using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
namespace Shared.WebHost.Security;
/// <summary>Limite por IP antes da validação criptográfica. O limiter global por usuário roda depois da autenticação.</summary>
internal sealed class IngressRateLimitMiddleware : IDisposable
{
    private readonly RequestDelegate next;
    private readonly PartitionedRateLimiter<HttpContext> limiter;
    public IngressRateLimitMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        this.next = next;
        var limit = configuration.GetValue("RateLimiting:IngressPermitLimit", 600);
        if (limit is < 1 or > 100000) throw new InvalidOperationException("RateLimiting:IngressPermitLimit inválido.");
        limiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    }
    public async Task InvokeAsync(HttpContext context)
    {
        using var lease = await limiter.AcquireAsync(context, cancellationToken: context.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.Response.Headers.RetryAfter = "60";
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
