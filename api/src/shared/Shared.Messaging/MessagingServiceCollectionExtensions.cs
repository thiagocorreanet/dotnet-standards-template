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
                o.HandlerTimeoutSeconds is >= 1 and <= 600 && o.ProcessedRetentionDays >= 7, "Outbox inválido")
            .ValidateOnStart();
        builder.Services.AddSingleton<OutboxRuntimeState>();
        builder.Services.AddSingleton<IntegrationEventTypeRegistry>();
        builder.Services.AddSingleton<IIntegrationEventPublisher, InProcessIntegrationEventPublisher>();
        builder.Services.AddHostedService<OutboxProcessor>();
        builder.Services.AddHostedService<OutboxProbe>();
        return builder;
    }

    /// <summary>Registra um handler de evento de integração (scoped).</summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        return services;
    }
}
