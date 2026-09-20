using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Contracts.Venues;
using Shared.Data;
using Shared.Http.Endpoints;
using Shared.WebHost;
using Shared.WebHost.Modules;

namespace Module.Venues.Shared;

/// <summary>Ponto de entrada do módulo Locais: serviços, contrato público e endpoints agrupados em <c>api/v1/venues</c>.</summary>
public sealed class VenuesModule : IModule
{
    public string Name => VenuesDbContext.SchemaName;
    public string RoutePrefix => "venues";
    public string Description => """
        Locais onde eventos presenciais acontecem e suas **salas** (ambientes).

        - Um local possui uma ou mais salas; um local de ambiente único nasce com a sala *"Ambiente único"*.
        - Salas são a unidade alocável para palestras (módulo Palestras consulta este módulo via `IVenuesModuleApi`).
        - Exclusões são lógicas (soft delete) e toda alteração gera trilha de auditoria.
        """;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<VenuesDbContext>(VenuesDbContext.SchemaName);
        builder.Services.AddScoped<IVenuesModuleApi, VenuesModuleApi>();
        builder.Services.AddScoped<IModuleAccessPolicy, VenuesAccessPolicy>();
        builder.Services.AddUseCasesFromAssembly(typeof(VenuesModule).Assembly, VenuesTelemetry.Instance);
        builder.Services.AddModuleValidators(typeof(VenuesModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapModuleGroup(RoutePrefix, Name).MapEndpointsFromAssembly(typeof(VenuesModule).Assembly);
}
