using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Integration;
using Shared.Messaging;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class EventConsumerTests
{
    [Fact]
    public void Inbox_consumer_outside_an_assembly_with_one_db_context_fails_at_registration()
    {
        // Este assembly de testes declara vários DbContexts de sonda: não há contexto de módulo inequívoco.
        var error = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddIntegrationEventHandler<ProbeEvent, InboxHandler>());

        error.Message.ShouldContain("[SkipInbox]");
    }

    [Fact]
    public void Skip_inbox_keeps_the_handler_without_db_context()
    {
        var consumer = EventConsumer.For(typeof(SkippedHandler));

        consumer.ContextType.ShouldBeNull();
        consumer.Name.ShouldBe(typeof(SkippedHandler).FullName);
    }

    [Fact]
    public void Skip_inbox_requires_a_justification() =>
        Should.Throw<InvalidOperationException>(() => EventConsumer.For(typeof(UnjustifiedHandler)));

    [Fact]
    public void Registered_handler_is_wrapped_and_keeps_its_type_for_telemetry()
    {
        using var provider = new ServiceCollection().AddIntegrationEventHandler<ProbeEvent, SkippedHandler>().BuildServiceProvider();

        var handler = provider.GetRequiredService<IIntegrationEventHandler<ProbeEvent>>();

        handler.ShouldBeAssignableTo<IEventConsumerDescriptor>()!.HandlerType.ShouldBe(typeof(SkippedHandler));
    }

    private sealed record ProbeEvent : IntegrationEvent;

    private sealed class InboxHandler : IIntegrationEventHandler<ProbeEvent>
    {
        public Task HandleAsync(ProbeEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SkipInbox("Sonda de teste sem efeito persistido.")]
    private sealed class SkippedHandler : IIntegrationEventHandler<ProbeEvent>
    {
        public Task HandleAsync(ProbeEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SkipInbox(" ")]
    private sealed class UnjustifiedHandler : IIntegrationEventHandler<ProbeEvent>
    {
        public Task HandleAsync(ProbeEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
