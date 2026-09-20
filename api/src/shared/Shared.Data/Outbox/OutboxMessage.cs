namespace Shared.Data.Outbox;
/// <summary>Entrega pelo menos uma vez. ClaimToken impede ACK de um consumidor que perdeu a concessão.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Type { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset OccurredOn { get; init; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? DeadLetteredAt { get; set; }
    public string? TraceParent { get; init; }
}
public sealed class OutboxReplayAudit : Shared.Data.Entities.IImmutableRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid MessageId { get; init; }
    public Guid ActorId { get; init; }
    public DateTimeOffset ReplayedAt { get; init; }
    public required string ReasonCode { get; init; }
}
public sealed record OutboxSnapshot(string Module, int Pending, int DeadLetters, double OldestPendingSeconds, DateTimeOffset ObservedAt);
public sealed record DeadLetter(Guid Id, string Type, int Attempts, string? Error, DateTimeOffset? DeadLetteredAt);
