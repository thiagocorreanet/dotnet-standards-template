using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Identity;
using Shared.Contracts.Integration;
using Shared.Data.Outbox;
using Shared.Messaging;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class OutboxSchedulingTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Slow_delivery_keeps_lease_and_does_not_block_other_module_or_snapshots()
    {
        var slow = new ControlledStore("slow");
        var fast = new ControlledStore("fast");
        var slowId = slow.Enqueue();
        fast.Enqueue();
        var entered = Signal();
        var release = Signal();
        using var host = CreateHost([slow, fast], async (message, ct) =>
        {
            if (message.Id != slowId) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
        });
        try
        {
            await host.StartAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await fast.Processed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await slow.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Eventually(() => slow.Probes >= 2 && fast.Probes >= 2);
            slow.Acks.ShouldBe(0);
            var state = host.Services.GetRequiredService<OutboxRuntimeState>();
            state.Read().Count.ShouldBe(2);
            state.Read().All(s => DateTimeOffset.UtcNow - s.ObservedAt < TimeSpan.FromSeconds(5)).ShouldBeTrue();
            release.TrySetResult();
            await slow.Processed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { release.TrySetResult(); await host.StopAsync(); }
    }

    [Fact]
    public async Task Delivery_capacity_is_bounded_and_claim_happens_only_after_capacity_is_available()
    {
        var stores = Enumerable.Range(0, 3).Select(i => new ControlledStore("module" + i)).ToArray();
        foreach (var store in stores) store.Enqueue();
        var entered = Signal();
        var release = Signal();
        var active = 0;
        using var host = CreateHost(stores, async (_, ct) =>
        {
            if (Interlocked.Increment(ref active) == 2) entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            Interlocked.Decrement(ref active);
        });
        try
        {
            await host.StartAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Eventually(() => stores.All(s => s.Probes >= 2));
            stores.Sum(s => s.Claims).ShouldBe(2);
            Volatile.Read(ref active).ShouldBe(2);
            release.TrySetResult();
            await Task.WhenAll(stores.Select(s => s.Processed.Task)).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { release.TrySetResult(); await host.StopAsync(); }
    }

    [Fact]
    public async Task Failed_probe_keeps_last_success_and_does_not_delay_other_probes()
    {
        var failing = new ControlledStore("failing");
        var healthy = new ControlledStore("healthy");
        using var host = CreateHost([failing, healthy], (_, _) => Task.CompletedTask);
        try
        {
            await host.StartAsync();
            var state = host.Services.GetRequiredService<OutboxRuntimeState>();
            await Eventually(() => state.Read().Count == 2);
            failing.BlockProbe = true;
            await failing.ProbeBlocked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var previous = state.Read().Single(s => s.Module == "failing").ObservedAt;
            await failing.ProbeCanceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Eventually(() => state.Read().Single(s => s.Module == "healthy").ObservedAt > previous);
            state.Read().Single(s => s.Module == "failing").ObservedAt.ShouldBe(previous);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Shutdown_cancels_inflight_delivery_without_acknowledging_or_rejecting_it()
    {
        var store = new ControlledStore("shutdown");
        store.Enqueue();
        var entered = Signal();
        var canceled = Signal();
        using var host = CreateHost([store], async (_, ct) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            finally { canceled.TrySetResult(); }
        });
        try
        {
            await host.StartAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        store.Acks.ShouldBe(0);
        store.Failures.ShouldBe(0);
    }

    [Fact]
    public async Task Handler_timeout_rejects_message_and_releases_capacity_for_the_next_delivery()
    {
        var store = new ControlledStore("timeout");
        var first = store.Enqueue();
        store.Enqueue();
        using var host = CreateHost([store], async (message, ct) =>
        {
            if (message.Id == first) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }, handlerTimeout: 1);
        try
        {
            await host.StartAsync();
            await store.Processed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            store.Failures.ShouldBe(1);
            store.Acks.ShouldBe(1);
        }
        finally { await host.StopAsync(); }
    }

    private static IHost CreateHost(IEnumerable<ControlledStore> stores, Func<IIntegrationEvent, CancellationToken, Task> publish, int handlerTimeout = 60)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.AddSharedMessaging();
        builder.Services.Configure<OutboxOptions>(o =>
        {
            o.PollingIntervalMs = 100;
            o.MaxConcurrentDeliveries = 2;
            o.LockSeconds = 9;
            o.ProbeIntervalSeconds = 1;
            o.ProbeTimeoutSeconds = 1;
            o.HandlerTimeoutSeconds = handlerTimeout;
        });
        foreach (var store in stores) builder.Services.AddSingleton<IOutboxStore>(store);
        builder.Services.AddSingleton<IIntegrationEventPublisher>(new ControlledPublisher(publish));
        return builder.Build();
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task Eventually(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(20, deadline.Token);
    }

    private sealed class ControlledPublisher(Func<IIntegrationEvent, CancellationToken, Task> publish) : IIntegrationEventPublisher
    {
        public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken) => publish(integrationEvent, cancellationToken);
    }

    private sealed class ControlledStore(string module) : IOutboxStore
    {
        private readonly ConcurrentQueue<OutboxMessage> messages = new();
        public string Module => module;
        public int Claims, Probes, Acks, Failures;
        public volatile bool BlockProbe;
        public TaskCompletionSource Processed { get; } = Signal();
        public TaskCompletionSource Renewed { get; } = Signal();
        public TaskCompletionSource ProbeBlocked { get; } = Signal();
        public TaskCompletionSource ProbeCanceled { get; } = Signal();
        public Guid Enqueue()
        {
            var message = new UserRegistered(Guid.NewGuid());
            messages.Enqueue(new OutboxMessage { Id = message.Id, Type = "identity.user-registered.v1",
                Payload = JsonSerializer.Serialize(message, WebJson),
                OccurredOn = message.OccurredOn, ClaimToken = Guid.NewGuid(), Attempts = 1 });
            return message.Id;
        }
        public Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(int batchSize, TimeSpan lockDuration, int maxAttempts, CancellationToken ct)
        {
            Interlocked.Increment(ref Claims);
            return Task.FromResult<IReadOnlyList<OutboxMessage>>(messages.TryDequeue(out var message) ? [message] : []);
        }
        public Task<bool> RenewAsync(Guid id, Guid claimToken, TimeSpan duration, CancellationToken ct) { Renewed.TrySetResult(); return Task.FromResult(true); }
        public Task<bool> MarkProcessedAsync(Guid id, Guid claimToken, CancellationToken ct)
        {
            Interlocked.Increment(ref Acks); Processed.TrySetResult(); return Task.FromResult(true);
        }
        public Task<bool> MarkFailedAsync(Guid id, Guid claimToken, string errorCode, TimeSpan retryDelay, int maxAttempts, CancellationToken ct)
        {
            Interlocked.Increment(ref Failures); return Task.FromResult(true);
        }
        public async Task<OutboxSnapshot> SnapshotAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Probes);
            if (BlockProbe)
            {
                ProbeBlocked.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                finally { ProbeCanceled.TrySetResult(); }
            }
            return new(Module, messages.Count, 0, 0, DateTimeOffset.UtcNow);
        }
        public Task<IReadOnlyList<DeadLetter>> ListDeadLettersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DeadLetter>>([]);
        public Task<bool> ReplayAsync(Guid id, Guid actorId, string reasonCode, CancellationToken ct) => Task.FromResult(false);
        public Task<int> PruneProcessedAsync(int retentionDays, CancellationToken ct) => Task.FromResult(0);
    }
}
