using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Contracts.People;
using Shared.Data;
using Shared.Http.Endpoints;
using Shared.WebHost;
using Shared.WebHost.Modules;

namespace Module.People.Shared;

/// <summary>Ponto de entrada do módulo Pessoas: serviços, contrato público e endpoints agrupados em <c>api/v1/people</c>.</summary>
public sealed class PeopleModule : IModule
{
    public string Name => PeopleDbContext.SchemaName;
    public string RoutePrefix => "people";
    public string Description => """
        Cadastro único de **pessoas**: palestrantes e participantes são Pessoas; o papel nasce do relacionamento com eventos e palestras.

        - E-mail é único entre pessoas ativas e armazenado em minúsculas; o CPF é armazenado apenas com dígitos e validado (dígitos verificadores).
        - Outros módulos consultam este via `IPeopleModuleApi` (resumo mínimo: id, nome e e-mail, apenas pessoas ativas).
        - Eventos de integração publicados: `PersonCreated` e `PersonDeleted`.
        - Exclusões são lógicas (soft delete) e toda alteração gera trilha de auditoria.
        """;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<PeopleDbContext>(PeopleDbContext.SchemaName);
        builder.Services.AddScoped<IPeopleModuleApi, PeopleModuleApi>();
        builder.Services.AddScoped<IModuleAccessPolicy, PeopleAccessPolicy>();
        builder.Services.AddUseCasesFromAssembly(typeof(PeopleModule).Assembly, PeopleTelemetry.Instance);
        builder.Services.AddModuleValidators(typeof(PeopleModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapModuleGroup(RoutePrefix, Name).MapEndpointsFromAssembly(typeof(PeopleModule).Assembly);
}
