using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Module.Identity.Shared;
using Shared.Contracts.Identity;
using Shared.Contracts.Integration;
using Shared.Data.Outbox;
using Shared.Messaging;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Reliability;

public sealed class OutboxReplicaTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Two_hosts_share_postgres_and_renew_slow_delivery_without_duplicate_claim_or_stale_probe()
    {
        await using var database = new ApiFactory { OutboxEnabled = false };
        await database.InitializeAsync();
        await database.WithServiceAsync(sp => sp.GetRequiredService<IdentityDbContext>().OutboxMessages.ExecuteDeleteAsync());
        var handler = new SlowHandler();
        void Configure(IWebHostBuilder builder) => builder.UseSetting("Outbox:Enabled", "true")
            .UseSetting("Database:MigrateOnStartup", "false")
            .UseSetting("Outbox:LockSeconds", "9")
            .UseSetting("Outbox:ProbeIntervalSeconds", "1")
            .ConfigureServices(s => s.AddSingleton<IIntegrationEventHandler<UserRegistered>>(handler));
        await using var first = database.WithWebHostBuilder(Configure);
        await using var second = database.WithWebHostBuilder(Configure);
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();
        var integrationEvent = new UserRegistered(Guid.NewGuid());
        try
        {
            await database.WithServiceAsync(async sp =>
            {
                var db = sp.GetRequiredService<IdentityDbContext>();
                db.OutboxMessages.Add(new OutboxMessage { Id = integrationEvent.Id, Type = "identity.user-registered.v1",
                    Payload = JsonSerializer.Serialize(integrationEvent, WebJson),
                    OccurredOn = integrationEvent.OccurredOn });
                return await db.SaveChangesAsync();
            });
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var claimed = await Message();
            claimed.ClaimToken.ShouldNotBeNull();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while ((await Message()).LockedUntil <= claimed.LockedUntil)
                await Task.Delay(100, deadline.Token);
            var renewed = await Message();
            renewed.ClaimToken.ShouldBe(claimed.ClaimToken);
            renewed.Attempts.ShouldBe(1);
            renewed.ProcessedOn.ShouldBeNull();
            Volatile.Read(ref handler.Calls).ShouldBe(1);
            foreach (var host in new[] { first, second })
            {
                var snapshot = host.Services.GetRequiredService<OutboxRuntimeState>().Read().Single(x => x.Module == "Identity");
                snapshot.ObservedAt.ShouldBeGreaterThan(integrationEvent.OccurredOn);
                (DateTimeOffset.UtcNow - snapshot.ObservedAt).ShouldBeLessThan(TimeSpan.FromSeconds(5));
            }
            handler.Release.TrySetResult();
            while ((await Message()).ProcessedOn is null) await Task.Delay(100, deadline.Token);
            (await Message()).Attempts.ShouldBe(1);
            Volatile.Read(ref handler.Calls).ShouldBe(1);
        }
        finally { handler.Release.TrySetResult(); }

        Task<OutboxMessage> Message() => database.WithServiceAsync(sp => sp.GetRequiredService<IdentityDbContext>()
            .OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == integrationEvent.Id));
    }

    private sealed class SlowHandler : IIntegrationEventHandler<UserRegistered>
    {
        public int Calls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task HandleAsync(UserRegistered integrationEvent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }
}
