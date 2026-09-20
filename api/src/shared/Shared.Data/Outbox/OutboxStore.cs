using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Transactions;
namespace Shared.Data.Outbox;
internal sealed class OutboxStore<TContext>(IServiceScopeFactory scopeFactory) : IOutboxStore
    where TContext : DbContext, IModuleDbContext
{
    public string Module => ModuleDbContext.ResolveModuleName(typeof(TContext));
    private static Task<DateTimeOffset> Now(DbContext db, CancellationToken ct) =>
        db.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);

    public async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(int batchSize, TimeSpan lockDuration, int maxAttempts, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = await Now(db, ct);
        // Inclui tentativas interrompidas por crash, não apenas erros capturados pelo processo.
        await db.OutboxMessages.Where(m => m.ProcessedOn == null && m.DeadLetteredAt == null &&
            m.Attempts >= maxAttempts && (m.LockedUntil == null || m.LockedUntil < now))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.DeadLetteredAt, now)
                .SetProperty(m => m.Error, "AttemptsExhausted").SetProperty(m => m.ClaimToken, (Guid?)null), ct);
        var candidates = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.ProcessedOn == null && m.DeadLetteredAt == null &&
                (m.NextAttemptAt == null || m.NextAttemptAt <= now) && (m.LockedUntil == null || m.LockedUntil < now))
            .OrderBy(m => m.OccurredOn).ThenBy(m => m.Id).Take(batchSize).Select(m => m.Id).ToListAsync(ct);
        if (candidates.Count == 0) return [];
        var token = Guid.NewGuid();
        await db.OutboxMessages.Where(m => candidates.Contains(m.Id) && m.ProcessedOn == null &&
                m.DeadLetteredAt == null && (m.LockedUntil == null || m.LockedUntil < now))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ClaimToken, token)
                .SetProperty(m => m.LockedUntil, now.Add(lockDuration)).SetProperty(m => m.Attempts, m => m.Attempts + 1), ct);
        return await db.OutboxMessages.AsNoTracking().Where(m => m.ClaimToken == token).OrderBy(m => m.OccurredOn).ToListAsync(ct);
    }
    public async Task<bool> RenewAsync(Guid id, Guid claimToken, TimeSpan duration, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = await Now(db, ct);
        return await db.OutboxMessages.Where(m => m.Id == id && m.ClaimToken == claimToken && m.ProcessedOn == null &&
            m.DeadLetteredAt == null && m.LockedUntil > now)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntil, now.Add(duration)), ct) == 1;
    }
    public async Task<bool> MarkProcessedAsync(Guid id, Guid claimToken, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = await Now(db, ct);
        return await db.OutboxMessages.Where(m => m.Id == id && m.ClaimToken == claimToken &&
            m.ProcessedOn == null && m.LockedUntil > now)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedOn, now)
                .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null).SetProperty(m => m.ClaimToken, (Guid?)null)
                .SetProperty(m => m.Error, (string?)null), ct) == 1;
    }
    public async Task<bool> MarkFailedAsync(Guid id, Guid claimToken, string errorCode, TimeSpan retryDelay, int maxAttempts, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = await Now(db, ct);
        return await db.OutboxMessages.Where(m => m.Id == id && m.ClaimToken == claimToken &&
            m.ProcessedOn == null && m.LockedUntil > now)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Error, errorCode.Substring(0, Math.Min(120, errorCode.Length)))
                .SetProperty(m => m.NextAttemptAt, now.Add(retryDelay))
                .SetProperty(m => m.DeadLetteredAt, m => m.Attempts >= maxAttempts ? now : null)
                .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null).SetProperty(m => m.ClaimToken, (Guid?)null), ct) == 1;
    }
    public async Task<OutboxSnapshot> SnapshotAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = await Now(db, ct);
        var pending = db.OutboxMessages.Where(m => m.ProcessedOn == null && m.DeadLetteredAt == null);
        var count = await pending.CountAsync(ct);
        var oldest = await pending.MinAsync(m => (DateTimeOffset?)m.OccurredOn, ct);
        var dead = await db.OutboxMessages.CountAsync(m => m.DeadLetteredAt != null, ct);
        return new(Module, count, dead, oldest.HasValue ? Math.Max(0, (now - oldest.Value).TotalSeconds) : 0, now);
    }
    public async Task<IReadOnlyList<DeadLetter>> ListDeadLettersAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        return await db.OutboxMessages.AsNoTracking().Where(m => m.DeadLetteredAt != null)
            .OrderBy(m => m.DeadLetteredAt).Take(100)
            .Select(m => new DeadLetter(m.Id, m.Type, m.Attempts, m.Error, m.DeadLetteredAt)).ToListAsync(ct);
    }
    public async Task<bool> ReplayAsync(Guid id, Guid actorId, string reasonCode, CancellationToken ct)
    {
        if (actorId == Guid.Empty || reasonCode.Length is < 3 or > 80 ||
            reasonCode.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new ArgumentException("Informe um código de chamado sem dados pessoais.");
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = await Now(db, ct);
        var changed = await db.OutboxMessages.Where(m => m.Id == id && m.DeadLetteredAt != null && m.ProcessedOn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.DeadLetteredAt, (DateTimeOffset?)null)
                .SetProperty(m => m.NextAttemptAt, now).SetProperty(m => m.Attempts, 0)
                .SetProperty(m => m.ClaimToken, (Guid?)null).SetProperty(m => m.LockedUntil, (DateTimeOffset?)null), ct);
        if (changed != 1) return false;
        db.Set<OutboxReplayAudit>().Add(new() { MessageId = id, ActorId = actorId, ReasonCode = reasonCode, ReplayedAt = now });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
    public async Task<int> PruneProcessedAsync(int retentionDays, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, 7);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var before = (await Now(db, ct)).AddDays(-retentionDays);
        // Nunca apagar pendentes/dead letters. Lotes limitados para não monopolizar o banco.
        var ids = await db.OutboxMessages.Where(m => m.ProcessedOn < before).OrderBy(m => m.ProcessedOn)
            .Take(1000).Select(m => m.Id).ToListAsync(ct);
        var deleted = await db.OutboxMessages.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync(ct);
        var receipts = await db.Set<CommandReceipt>().Where(m => m.CommittedAt < before)
            .OrderBy(m => m.CommittedAt).Take(1000).Select(m => m.Id).ToListAsync(ct);
        await db.Set<CommandReceipt>().Where(m => receipts.Contains(m.Id)).ExecuteDeleteAsync(ct);
        return deleted;
    }
}
