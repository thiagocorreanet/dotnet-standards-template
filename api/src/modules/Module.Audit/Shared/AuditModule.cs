using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Module.Audit.Shared.Handlers;
using Shared.Contracts.Audit;
using Shared.Data;
using Shared.Http.Endpoints;
using Shared.Messaging;
using Shared.WebHost;
using Shared.WebHost.Modules;

namespace Module.Audit.Shared;

/// <summary>Ponto de entrada do módulo Auditoria: contexto, handler do evento <c>EntityChanged</c> e endpoints em <c>api/v1/audit</c>.</summary>
public sealed class AuditModule : IModule
{
    public string Name => AuditDbContext.SchemaName;
    public string RoutePrefix => "audit";
    public string Description => """
        Trilha de auditoria **centralizada e imutável** de todas as alterações de entidades do sistema, consultável apenas por Administradores.

        **Fluxo (assíncrono, sem acoplamento entre módulos):**
        1. O `AuditSaveChangesInterceptor` (Shared.Data) gera um evento `EntityChanged` para cada Insert/Update/Delete (soft) de entidade auditável, com os valores anteriores e novos em JSON.
        2. O evento é gravado na tabela `OutboxMessages` **do schema do módulo de origem**, na mesma transação da alteração de negócio.
        3. O `OutboxProcessor` (Shared.Messaging) lê os Outboxes de todos os módulos e publica os eventos in-process.
        4. O `EntityChangedHandler` deste módulo persiste o registro na tabela `Audit.AuditRecords`, usando o Id do evento como chave (idempotente diante de reentregas).

        - Registros nunca são alterados nem excluídos; `recordedAt - occurredOn` mede a latência do Outbox.
        - Este contexto não gera auditoria de si mesmo (`AuditChangesEnabled = false`).
        - Operações: `Insert`, `Update`, `Delete`.
        """;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<AuditDbContext>(AuditDbContext.SchemaName);
        builder.Services.AddIntegrationEventHandler<EntityChanged, EntityChangedHandler>();
        builder.Services.AddScoped<IModuleAccessPolicy, AuditAccessPolicy>();
        builder.Services.AddUseCasesFromAssembly(typeof(AuditModule).Assembly, AuditTelemetry.Instance);
        builder.Services.AddModuleValidators(typeof(AuditModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapModuleGroup(RoutePrefix, Name).MapEndpointsFromAssembly(typeof(AuditModule).Assembly);
}
