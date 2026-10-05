using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Module.Identity.Shared;
using Shared.Contracts.Integration;
using Shared.Data.Inbox;
using Shared.Data.Transactions;
using Shared.Messaging;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Reliability;

/// <summary>
/// Inbox com PostgreSQL real, no schema Identity já migrado. O efeito do handler de teste é uma linha em
/// <c>CommandReceipts</c> com o Id do evento: dá para contar o efeito no banco, na mesma transação da Inbox.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class InboxTests(ApiFactory factory)
{
    [Fact]
    public async Task Repeated_delivery_applies_the_effect_once()
    {
        var probe = new ProbeEvent();
        var consumer = Consumer<ProbeHandler>("tests.inbox.repeated");

        await consumer.HandleAsync(probe, CancellationToken.None);
        await consumer.HandleAsync(probe, CancellationToken.None);

        ProbeHandler.Calls(probe).ShouldBe(1);
        (await Effects(probe)).ShouldBe(1);
        (await InboxEntries(probe)).ShouldBe(["tests.inbox.repeated"]);
    }

    [Fact]
    public async Task Failed_handler_rolls_back_effect_and_inbox_entry_so_the_retry_applies_it()
    {
        var probe = new ProbeEvent();
        ProbeHandler.FailNext(probe);
        var consumer = Consumer<ProbeHandler>("tests.inbox.retry");

        await Should.ThrowAsync<InvalidOperationException>(() => consumer.HandleAsync(probe, CancellationToken.None));
        (await Effects(probe)).ShouldBe(0);
        (await InboxEntries(probe)).ShouldBeEmpty();

        await consumer.HandleAsync(probe, CancellationToken.None);
        (await Effects(probe)).ShouldBe(1);
        (await InboxEntries(probe)).ShouldBe(["tests.inbox.retry"]);
    }

    [Fact]
    public async Task Concurrent_deliveries_apply_the_effect_once()
    {
        var probe = new ProbeEvent();
        var consumer = Consumer<ProbeHandler>("tests.inbox.concurrent");

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => consumer.HandleAsync(probe, CancellationToken.None))));

        ProbeHandler.Calls(probe).ShouldBe(1);
        (await Effects(probe)).ShouldBe(1);
    }

    [Fact]
    public async Task Each_consumer_of_the_same_event_is_recorded_independently()
    {
        var probe = new ProbeEvent();

        await Consumer<ProbeHandler>("tests.inbox.first").HandleAsync(probe, CancellationToken.None);
        await Consumer<SecondProbeHandler>("tests.inbox.second").HandleAsync(probe, CancellationToken.None);
        await Consumer<SecondProbeHandler>("tests.inbox.second").HandleAsync(probe, CancellationToken.None);

        ProbeHandler.Calls(probe).ShouldBe(1);
        (await Effects(probe)).ShouldBe(1);
        (await factory.WithServiceAsync(sp => sp.GetRequiredService<IdentityDbContext>().Set<CommandReceipt>()
            .CountAsync(r => r.Id == SecondProbeHandler.EffectId(probe)))).ShouldBe(1);
        (await InboxEntries(probe)).Order().ShouldBe(["tests.inbox.first", "tests.inbox.second"]);
    }

    private IsolatedEventConsumer<ProbeEvent, THandler> Consumer<THandler>(string name)
        where THandler : class, IIntegrationEventHandler<ProbeEvent> =>
        new(factory.Services.GetRequiredService<IServiceScopeFactory>(), new EventConsumer(typeof(THandler), name, typeof(IdentityDbContext)));

    private Task<int> Effects(ProbeEvent probe) => factory.WithServiceAsync(sp =>
        sp.GetRequiredService<IdentityDbContext>().Set<CommandReceipt>().CountAsync(r => r.Id == probe.Id));

    private Task<List<string>> InboxEntries(ProbeEvent probe) => factory.WithServiceAsync(sp =>
        sp.GetRequiredService<IdentityDbContext>().Set<InboxMessage>().Where(m => m.EventId == probe.Id)
            .Select(m => m.Consumer).ToListAsync());

    private sealed record ProbeEvent : IntegrationEvent;

    /// <summary>Outro consumidor do mesmo evento, com efeito próprio.</summary>
    private sealed class SecondProbeHandler(IdentityDbContext db) : IIntegrationEventHandler<ProbeEvent>
    {
        public static Guid EffectId(ProbeEvent probe) => new([.. probe.Id.ToByteArray().Reverse()]);

        public async Task HandleAsync(ProbeEvent integrationEvent, CancellationToken cancellationToken)
        {
            db.Set<CommandReceipt>().Add(new CommandReceipt { Id = EffectId(integrationEvent), CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Grava o efeito no contexto do módulo, que participa da transação aberta pela Inbox.</summary>
    private sealed class ProbeHandler(IdentityDbContext db) : IIntegrationEventHandler<ProbeEvent>
    {
        private static readonly ConcurrentDictionary<Guid, int> Executions = new();
        private static readonly ConcurrentDictionary<Guid, bool> Failures = new();

        public static int Calls(ProbeEvent probe) => Executions.GetValueOrDefault(probe.Id);

        public static void FailNext(ProbeEvent probe) => Failures[probe.Id] = true;

        public async Task HandleAsync(ProbeEvent integrationEvent, CancellationToken cancellationToken)
        {
            db.Set<CommandReceipt>().Add(new CommandReceipt { Id = integrationEvent.Id, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
            await Task.Delay(50, cancellationToken);
            if (Failures.TryRemove(integrationEvent.Id, out _))
                throw new InvalidOperationException("Falha simulada depois de gravar o efeito.");
            Executions.AddOrUpdate(integrationEvent.Id, 1, (_, count) => count + 1);
        }
    }
}
