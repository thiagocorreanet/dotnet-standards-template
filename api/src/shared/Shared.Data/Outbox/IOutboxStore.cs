namespace Shared.Data.Outbox;
public interface IOutboxStore
{
    string Module { get; }
    Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(int batchSize, TimeSpan lockDuration, int maxAttempts, CancellationToken ct);
    Task<bool> RenewAsync(Guid id, Guid claimToken, TimeSpan duration, CancellationToken ct);
    Task<bool> MarkProcessedAsync(Guid id, Guid claimToken, CancellationToken ct);
    Task<bool> MarkFailedAsync(Guid id, Guid claimToken, string errorCode, TimeSpan retryDelay, int maxAttempts, CancellationToken ct);
    Task<OutboxSnapshot> SnapshotAsync(CancellationToken ct);
    Task<IReadOnlyList<DeadLetter>> ListDeadLettersAsync(CancellationToken ct);
    Task<bool> ReplayAsync(Guid id, Guid actorId, string reasonCode, CancellationToken ct);
    Task<int> PruneProcessedAsync(int retentionDays, CancellationToken ct);
}
