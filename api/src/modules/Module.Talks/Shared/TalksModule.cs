using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Contracts.Talks;
using Shared.Data;
using Shared.Http.Endpoints;
using Shared.WebHost;
using Shared.WebHost.Modules;

namespace Module.Talks.Shared;

/// <summary>Ponto de entrada do módulo Palestras: serviços, contrato público e endpoints agrupados em <c>api/v1/talks</c>.</summary>
public sealed class TalksModule : IModule
{
    public string Name => TalksDbContext.SchemaName;
    public string RoutePrefix => "talks";
    public string Description => """
        Palestras de um evento, seus **palestrantes**, **conteúdos**, **presenças** e **certificados**.

        - Uma palestra pertence a um evento (módulo Eventos) e pode ocupar uma sala do local do evento (módulo Locais); a agenda da sala não admite sobreposição.
        - Palestrantes e participantes são pessoas (módulo Pessoas); a palestra mantém sempre ao menos um palestrante.
        - Presença exige inscrição confirmada no evento; o certificado exige presença e palestra encerrada, e é validável publicamente pelo código.
        - Exclusões são lógicas (soft delete) e toda alteração gera trilha de auditoria.
        """;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<TalksDbContext>(TalksDbContext.SchemaName);
        builder.Services.AddScoped<ITalksModuleApi, TalksModuleApi>();
        builder.Services.AddScoped<TalkScheduleChecker>();
        builder.Services.AddScoped<IModuleAccessPolicy, TalksAccessPolicy>();
        builder.Services.AddUseCasesFromAssembly(typeof(TalksModule).Assembly, TalksTelemetry.Instance);
        builder.Services.AddModuleValidators(typeof(TalksModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapModuleGroup(RoutePrefix, Name).MapEndpointsFromAssembly(typeof(TalksModule).Assembly);
}
