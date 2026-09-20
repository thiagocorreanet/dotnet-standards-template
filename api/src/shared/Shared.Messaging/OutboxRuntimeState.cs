using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Shared.Data.Outbox;
namespace Shared.Messaging;
/// <summary>Callbacks de métricas leem memória; a coleta nunca consulta PostgreSQL.</summary>
public sealed class OutboxRuntimeState
{
    private readonly ConcurrentDictionary<string, OutboxSnapshot> snapshots = new();
    private readonly Meter meter = new("ModularApi.Messaging");
    public OutboxRuntimeState()
    {
        meter.CreateObservableGauge("outbox.pending", () => snapshots.Values.Select(s => Measure(s.Pending, s)));
        meter.CreateObservableGauge("outbox.dead_letters", () => snapshots.Values.Select(s => Measure(s.DeadLetters, s)));
        meter.CreateObservableGauge("outbox.oldest_pending_seconds", () => snapshots.Values.Select(s => Measure(s.OldestPendingSeconds, s)));
        meter.CreateObservableGauge("outbox.probe_age_seconds", () => snapshots.Values.Select(s => Measure((DateTimeOffset.UtcNow - s.ObservedAt).TotalSeconds, s)));
    }
    private static Measurement<double> Measure(double value, OutboxSnapshot s) => new(value, new KeyValuePair<string, object?>("module", s.Module));
    public void Update(OutboxSnapshot snapshot) => snapshots[snapshot.Module] = snapshot;
    public IReadOnlyList<OutboxSnapshot> Read() => snapshots.Values.OrderBy(s => s.Module).ToArray();
}
