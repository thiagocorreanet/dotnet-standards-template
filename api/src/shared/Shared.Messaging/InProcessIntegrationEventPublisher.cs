using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Integration;

namespace Shared.Messaging;

/// <summary>
/// Publicador in-process: resolve <c>IIntegrationEventHandler&lt;T&gt;</c> em um escopo DI próprio por mensagem,
/// com span de trace por entrega. Falha em um handler não impede os demais, mas marca a mensagem para retry.
/// </summary>
internal sealed class InProcessIntegrationEventPublisher(IServiceScopeFactory scopeFactory, ILogger<InProcessIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    private static readonly ActivitySource ActivitySource = new("ModularApi.Messaging");
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethods = new();

    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var typeEvent = integrationEvent.GetType();
        var typeHandler = typeof(IIntegrationEventHandler<>).MakeGenericType(typeEvent);

        await using var scope = scopeFactory.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices(typeHandler).Where(h => h is not null).ToList();
        if (handlers.Count == 0)
        {
            if (EventContractAttribute.For(typeEvent).RequiresConsumer)
                throw new InvalidOperationException("RequiredConsumerMissing");
            logger.LogDebug("Nenhum handler registrado para {IntegrationEvent}", typeEvent.Name);
            return;
        }

        var failures = new List<Exception>();
        foreach (var handler in handlers)
        {
            using var activity = ActivitySource.StartActivity($"consume {typeEvent.Name}", ActivityKind.Consumer);
            activity?.SetTag("messaging.event.type", typeEvent.FullName);
            activity?.SetTag("messaging.event.id", integrationEvent.Id);
            activity?.SetTag("messaging.handler", handler!.GetType().FullName);
            try
            {
                var method = HandleMethods.GetOrAdd(typeHandler, t => t.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!);
                await (Task)method.Invoke(handler, [integrationEvent, cancellationToken])!;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                logger.LogError("Falha no handler {Handler} para {IntegrationEvent} {EventId}: {ErrorType}", handler!.GetType().Name, typeEvent.Name, integrationEvent.Id, ex.GetType().Name);
                failures.Add(ex);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException($"Falha em {failures.Count} handler(s) de {typeEvent.Name}", failures);
        }
    }
}
