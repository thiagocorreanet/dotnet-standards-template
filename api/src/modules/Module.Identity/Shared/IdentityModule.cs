using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Contracts.Identity;
using Shared.Data;
using Shared.Http.Endpoints;
using Shared.WebHost;
using Shared.WebHost.Modules;
namespace Module.Identity.Shared;
public sealed class IdentityModule : IModule
{
    public string Name => IdentityDbContext.SchemaName;
    public string RoutePrefix => "identity";
    public string Description => "Vínculos locais de identidades OIDC. Senhas, MFA, sessões e perfis são administrados no provedor Keycloak.";
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<IdentityDbContext>(IdentityDbContext.SchemaName);
        builder.Services.AddScoped<IIdentityResolver, IdentityResolver>();
        builder.Services.AddScoped<IIdentityBootstrapper, IdentityBootstrapper>();
        builder.Services.AddScoped<IModuleAccessPolicy, IdentityAccessPolicy>();
        builder.Services.AddUseCasesFromAssembly(typeof(IdentityModule).Assembly, IdentityTelemetry.Instance);
        builder.Services.AddModuleValidators(typeof(IdentityModule).Assembly);
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapModuleGroup(RoutePrefix, Name).MapEndpointsFromAssembly(typeof(IdentityModule).Assembly);
}
