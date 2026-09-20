using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Data.Outbox;

namespace Shared.Messaging;

/// <summary>Sondas independentes de entrega/limpeza e entre módulos. Falha não renova snapshot antigo.</summary>
internal sealed class OutboxProbe(IEnumerable<IOutboxStore> stores, OutboxRuntimeState state,
    IOptions<OutboxOptions> options, ILogger<OutboxProbe> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => options.Value.Enabled
        ? Task.WhenAll(stores.Select(store => ProbeAsync(store, stoppingToken)))
        : Task.CompletedTask;

    private async Task ProbeAsync(IOutboxStore store, CancellationToken stoppingToken)
    {
        var cfg = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(cfg.ProbeTimeoutSeconds));
                state.Update(await store.SnapshotAsync(deadline.Token));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sonda Outbox {Module} falhou", store.Module);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(cfg.ProbeIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
