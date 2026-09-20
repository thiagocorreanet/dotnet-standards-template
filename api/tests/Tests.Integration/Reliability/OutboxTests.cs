using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Module.Identity.Shared;
using Module.Audit.Shared;
using Shared.Contracts.Audit;
using Shared.Data.Outbox;
using Shared.Messaging;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;
namespace Tests.Integration.Reliability;
public sealed class OutboxTests : IAsyncLifetime
{
    private readonly ApiFactory factory = new() { OutboxEnabled = false };
    private IOutboxStore Store => factory.Services.GetServices<IOutboxStore>().Single(s => s.Module == "Identity");
    public async Task InitializeAsync()
    {
        await factory.InitializeAsync();
        await factory.WithServiceAsync(sp => sp.GetRequiredService<IdentityDbContext>().OutboxMessages.ExecuteDeleteAsync());
    }
    public Task DisposeAsync() => factory.DisposeAsync();
    private Task<Guid> Add(DateTimeOffset? occurred = null) => factory.WithServiceAsync(async sp =>
    {
        var db = sp.GetRequiredService<IdentityDbContext>();
        var message = new OutboxMessage { Type = "identity.user-registered.v1", Payload = "{}", OccurredOn = occurred ?? DateTimeOffset.UtcNow };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    });
    [Fact]
    public async Task Claim_exclusive_between_workers_and_ACK_fenced_by_token()
    {
        var id = await Add();
        var races = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 3, default)));
        var message = races.SelectMany(x => x).ShouldHaveSingleItem();
        message.Id.ShouldBe(id);
        (await Store.RenewAsync(id, message.ClaimToken!.Value, TimeSpan.FromSeconds(30), default)).ShouldBeTrue();
        (await Store.MarkProcessedAsync(id, Guid.NewGuid(), default)).ShouldBeFalse();
        (await Store.MarkProcessedAsync(id, message.ClaimToken.Value, default)).ShouldBeTrue();
        (await Store.MarkProcessedAsync(id, message.ClaimToken.Value, default)).ShouldBeFalse();
    }
    [Fact]
    public async Task Previous_consumer_cannot_acknowledge_or_change_another_consumers_claim()
    {
        var id = await Add();
        var old = (await Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 5, default)).Single();
        await factory.WithServiceAsync(sp => sp.GetRequiredService<IdentityDbContext>().OutboxMessages.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntil, DateTimeOffset.UtcNow.AddMinutes(-1))));
        var current = (await Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 5, default)).Single();
        current.ClaimToken.ShouldNotBe(old.ClaimToken);
        (await Store.RenewAsync(id, old.ClaimToken!.Value, TimeSpan.FromSeconds(30), default)).ShouldBeFalse();
        (await Store.MarkProcessedAsync(id, old.ClaimToken.Value, default)).ShouldBeFalse();
        (await Store.MarkFailedAsync(id, old.ClaimToken.Value, "Stale", TimeSpan.Zero, 1, default)).ShouldBeFalse();
        (await Store.MarkProcessedAsync(id, current.ClaimToken!.Value, default)).ShouldBeTrue();
    }
    [Fact]
    public async Task Failure_terminal_and_replay_are_states_explicit_with_audit()
    {
        var id = await Add();
        var message = (await Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 1, default)).Single();
        (await Store.MarkFailedAsync(id, message.ClaimToken!.Value, "KnownFailure", TimeSpan.Zero, 1, default)).ShouldBeTrue();
        (await Store.SnapshotAsync(default)).DeadLetters.ShouldBe(1);
        (await Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 1, default)).ShouldBeEmpty();
        (await Store.ReplayAsync(id, factory.DefaultAccountId, "INC-123", default)).ShouldBeTrue();
        (await Store.ReplayAsync(id, factory.DefaultAccountId, "INC-123", default)).ShouldBeFalse();
        await factory.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<IdentityDbContext>();
            var audit = await db.Set<OutboxReplayAudit>().SingleAsync(a => a.MessageId == id);
            audit.ActorId.ShouldBe(factory.DefaultAccountId);
            audit.ReasonCode.ShouldBe("INC-123");
            return true;
        });
        (await Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 1, default)).ShouldHaveSingleItem().Attempts.ShouldBe(1);
    }
    [Fact]
    public async Task Crash_on_last_attempt_also_moves_message_to_dead_letter()
    {
        var id = await Add();
        await Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 1, default);
        await factory.WithServiceAsync(sp => sp.GetRequiredService<IdentityDbContext>().OutboxMessages.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntil, DateTimeOffset.UtcNow.AddSeconds(-1))));
        (await Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 1, default)).ShouldBeEmpty();
        (await Store.ListDeadLettersAsync(default)).ShouldHaveSingleItem().Error.ShouldBe("AttemptsExhausted");
    }
    [Fact]
    public async Task Retention_deletes_only_processed_messages_and_preserves_pending_messages()
    {
        var pending = await Add(DateTimeOffset.UtcNow.AddDays(-30));
        var processed = await Add(DateTimeOffset.UtcNow.AddDays(-30));
        await factory.WithServiceAsync(sp => sp.GetRequiredService<IdentityDbContext>().OutboxMessages.Where(m => m.Id == processed)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedOn, DateTimeOffset.UtcNow.AddDays(-20))));
        (await Store.PruneProcessedAsync(7, default)).ShouldBe(1);
        (await Store.SnapshotAsync(default)).Pending.ShouldBe(1);
        (await Store.ClaimBatchAsync(1, TimeSpan.FromSeconds(30), 1, default)).Single().Id.ShouldBe(pending);
    }
    [Fact]
    public async Task Audit_consumes_event_concurrent_a_single_time()
    {
        var e = new EntityChanged("Identity", "User", Guid.NewGuid().ToString(), "Update", null,
            "{\"Email\":\"***\"}", factory.DefaultAccountId, factory.DefaultAccountId.ToString(), null);
        var publisher = factory.Services.GetRequiredService<IIntegrationEventPublisher>();
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => publisher.PublishAsync(e, default)));
        await factory.WithServiceAsync(async sp =>
        {
            (await sp.GetRequiredService<AuditDbContext>().AuditRecords.CountAsync(r => r.Id == e.Id)).ShouldBe(1);
            return true;
        });
    }
}
