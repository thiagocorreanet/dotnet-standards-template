using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Shared.Http.Endpoints;
using Shared.WebHost.Modules;
using Shared.WebHost.Security;

namespace Module.RateLimitProbe;

/// <summary>
/// Módulo de teste com o padrão documentado em <c>docs/extending.md</c>: a política nomeada é registrada em
/// <see cref="ConfigureServices"/> e aplicada no endpoint com <c>RequireRateLimiting</c>.
/// </summary>
public sealed class RateLimitProbeModule : IModule
{
    public const string PolicyName = "rate-limit-probe";
    public const int PermitLimit = 2;

    public string Name => "RateLimitProbe";
    public string RoutePrefix => "rate-limit-probe";
    public string Description => "Módulo de teste para a política nomeada de rate limiting.";

    public void ConfigureServices(IHostApplicationBuilder builder) =>
        builder.AddFixedWindowRateLimitPolicy(PolicyName, PermitLimit, windowSeconds: 60);

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapModuleGroup(RoutePrefix, Name)
            .MapGet("/ping", () => Results.NoContent())
            .WithName("RateLimitProbePing")
            .WithSummary("Sonda de teste com política nomeada de rate limiting")
            .RequireRateLimiting(PolicyName)
            .Produces(StatusCodes.Status204NoContent);
}
