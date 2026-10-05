using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Shared.Data;
using Shared.Http.Endpoints;
using Shared.WebHost;
using Shared.WebHost.Modules;

namespace Module.ModuleName.Shared;

/// <summary>Ponto de entrada do módulo ModuleName: contexto, casos de uso e endpoints em <c>api/v1/module-route</c>.</summary>
public sealed class ModuleNameModule : IModule
{
    public string Name => ModuleNameDbContext.SchemaName;
    public string RoutePrefix => "module-route";
    public string Description => """
        Descreva aqui, em pt-BR, o que o módulo ModuleName resolve, suas regras principais e os eventos que publica.
        """;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<ModuleNameDbContext>(ModuleNameDbContext.SchemaName);
        builder.Services.AddUseCasesFromAssembly(typeof(ModuleNameModule).Assembly, ModuleNameTelemetry.Instance);
        builder.Services.AddModuleValidators(typeof(ModuleNameModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapModuleGroup(RoutePrefix, Name).MapEndpointsFromAssembly(typeof(ModuleNameModule).Assembly);
}
