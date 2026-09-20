namespace Shared.Messaging;
public sealed class OutboxOptions
{
    public const string Section = "Outbox";
    public bool Enabled { get; set; } = true;
    public int PollingIntervalMs { get; set; } = 1000;
    public int BatchSize { get; set; } = 20;
    public int MaxConcurrentDeliveries { get; set; } = 4;
    public int ProbeIntervalSeconds { get; set; } = 5;
    public int ProbeTimeoutSeconds { get; set; } = 5;
    public int LockSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 10;
    public int HandlerTimeoutSeconds { get; set; } = 120;
    public int ProcessedRetentionDays { get; set; } = 7;
}
