using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Contracts.Integration;

namespace Shared.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Publicador in-process + processador do Outbox. Registre no host que deve consumir o Outbox (Api ou Worker).</summary>
    public static IHostApplicationBuilder AddSharedMessaging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.Section)
            .Validate(o => o.PollingIntervalMs is >= 100 and <= 60000 && o.BatchSize is >= 1 and <= 100 &&
                o.MaxConcurrentDeliveries is >= 1 and <= 32 && o.ProbeIntervalSeconds is >= 1 and <= 10 &&
                o.ProbeTimeoutSeconds is >= 1 and <= 10 &&
                o.LockSeconds is >= 9 and <= 600 && o.MaxAttempts is >= 1 and <= 30 &&
                o.HandlerTimeoutSeconds is >= 1 and <= 600 && o.ProcessedRetentionDays >= 7 &&
                o.InboxRetentionDays >= o.ProcessedRetentionDays, "Outbox inválido")
            .ValidateOnStart();
        builder.Services.AddSingleton<OutboxRuntimeState>();
        builder.Services.AddSingleton<IntegrationEventTypeRegistry>();
        builder.Services.AddSingleton<IIntegrationEventPublisher, InProcessIntegrationEventPublisher>();
        builder.Services.AddHostedService<OutboxProcessor>();
        builder.Services.AddHostedService<OutboxProbe>();
        return builder;
    }

    /// <summary>
    /// Registra um handler de evento de integração. Cada execução roda em escopo DI próprio e, por padrão, passa pela
    /// Inbox do módulo do handler: o par <c>(EventId, consumidor)</c> é gravado na mesma transação do efeito, e a
    /// repetição da mensagem não reaplica o handler. Use <see cref="SkipInboxAttribute"/> para abrir mão disso.
    /// </summary>
    /// <exception cref="InvalidOperationException">Handler com Inbox fora de um assembly com exatamente um DbContext.</exception>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        var consumer = EventConsumer.For(typeof(THandler));
        services.AddScoped<THandler>();
        services.AddSingleton<IIntegrationEventHandler<TEvent>>(sp =>
            new IsolatedEventConsumer<TEvent, THandler>(sp.GetRequiredService<IServiceScopeFactory>(), consumer));
        return services;
    }
}
