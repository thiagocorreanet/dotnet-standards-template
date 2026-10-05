using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Integration;
using Shared.Data.Inbox;

namespace Shared.Messaging;

internal static class MessagingTelemetry
{
    private static readonly Meter Meter = new("ModularApi.Messaging");

    /// <summary>Entregas repetidas ignoradas pela Inbox, rotuladas pelo nome do consumidor (conjunto fixo no código).</summary>
    public static readonly Counter<long> InboxDuplicates = Meter.CreateCounter<long>("inbox.duplicates.skipped");
}

/// <summary>Handler real por trás do invólucro; usado pelo publicador em logs e traces.</summary>
internal interface IEventConsumerDescriptor
{
    Type HandlerType { get; }
}

/// <summary>Consumidor resolvido no registro: falha de configuração aparece no startup.</summary>
internal sealed record EventConsumer(Type HandlerType, string Name, Type? ContextType)
{
    public static EventConsumer For(Type handlerType)
    {
        if (handlerType.GetCustomAttribute<SkipInboxAttribute>() is { } skip)
        {
            if (string.IsNullOrWhiteSpace(skip.Justification))
                throw new InvalidOperationException($"[SkipInbox] em {handlerType.Name} exige justificativa.");
            return new(handlerType, handlerType.FullName!, null);
        }

        var name = handlerType.GetCustomAttribute<InboxConsumerAttribute>()?.Name ?? handlerType.FullName!;
        if (string.IsNullOrWhiteSpace(name) || name.Length > InboxRecorder.MaxConsumerLength)
            throw new InvalidOperationException($"Nome de consumidor inválido em {handlerType.Name}: 1 a {InboxRecorder.MaxConsumerLength} caracteres.");
        var contexts = handlerType.Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(DbContext).IsAssignableFrom(t)).ToArray();
        if (contexts.Length != 1)
            throw new InvalidOperationException(
                $"{handlerType.Name} usa Inbox e precisa estar no assembly de um módulo com exatamente um DbContext, ou declarar [SkipInbox].");
        return new(handlerType, name, contexts[0]);
    }
}

/// <summary>
/// Executa um handler em escopo DI próprio. Com Inbox, abre a transação no contexto do módulo consumidor, registra
/// <c>(EventId, Consumer)</c> e só então chama o handler; efeito e registro confirmam juntos. Evento já registrado
/// para o consumidor é ignorado, então a repetição da mensagem não reaplica handlers que já terminaram.
/// </summary>
internal sealed class IsolatedEventConsumer<TEvent, THandler>(IServiceScopeFactory scopeFactory, EventConsumer consumer)
    : IIntegrationEventHandler<TEvent>, IEventConsumerDescriptor
    where TEvent : IIntegrationEvent
    where THandler : class, IIntegrationEventHandler<TEvent>
{
    public Type HandlerType => typeof(THandler);

    public async Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = ActivatorUtilities.GetServiceOrCreateInstance<THandler>(scope.ServiceProvider);
        if (consumer.ContextType is null)
        {
            await handler.HandleAsync(integrationEvent, cancellationToken);
            return;
        }

        var db = (DbContext)scope.ServiceProvider.GetRequiredService(consumer.ContextType);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await InboxRecorder.TryRecordAsync(db, integrationEvent.Id, consumer.Name, cancellationToken))
            await handler.HandleAsync(integrationEvent, cancellationToken);
        else
            MessagingTelemetry.InboxDuplicates.Add(1, new KeyValuePair<string, object?>("consumer", consumer.Name));
        await transaction.CommitAsync(cancellationToken);
    }
}
