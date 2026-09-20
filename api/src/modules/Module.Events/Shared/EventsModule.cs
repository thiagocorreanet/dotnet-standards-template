using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Contracts.Events;
using Shared.Data;
using Shared.Http.Endpoints;
using Shared.WebHost;
using Shared.WebHost.Modules;

namespace Module.Events.Shared;

/// <summary>Ponto de entrada do módulo Eventos: serviços, contrato público e endpoints agrupados em <c>api/v1/events</c>.</summary>
public sealed class EventsModule : IModule
{
    public string Name => EventsDbContext.SchemaName;
    public string RoutePrefix => "events";
    public string Description => """
        Eventos (presenciais, remotos ou híbridos) e as **inscrições** de participantes.

        - Um evento nasce em `Draft` e segue o ciclo `Published` → `InProgress` → `Closed`, podendo ser `Canceled` antes de encerrar.
        - Publicar exige ao menos uma palestra (consulta ao módulo Palestras via `ITalksModuleApi`).
        - Eventos presenciais/híbridos referenciam um local do módulo Locais (`IVenuesModuleApi`); a capacidade do evento, quando não informada, é a do local.
        - Inscrições referenciam pessoas do módulo Pessoas (`IPeopleModuleApi`); o cancelamento mantém o registro com situação `Canceled`.
        - Emite os eventos de integração `EventPublished`, `EventCanceled`, `RegistrationCompleted` e `RegistrationCanceled` via Outbox.
        """;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<EventsDbContext>(EventsDbContext.SchemaName);
        builder.Services.AddScoped<IEventsModuleApi, EventsModuleApi>();
        builder.Services.AddScoped<IModuleAccessPolicy, EventsAccessPolicy>();
        builder.Services.AddUseCasesFromAssembly(typeof(EventsModule).Assembly, EventsTelemetry.Instance);
        builder.Services.AddModuleValidators(typeof(EventsModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapModuleGroup(RoutePrefix, Name).MapEndpointsFromAssembly(typeof(EventsModule).Assembly);
}
