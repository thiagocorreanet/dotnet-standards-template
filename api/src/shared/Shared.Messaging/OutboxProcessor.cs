using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Contracts.Integration;
using Shared.Data.Outbox;
namespace Shared.Messaging;
internal sealed class OutboxProcessor(
    IEnumerable<IOutboxStore> stores, IIntegrationEventPublisher publisher,
    IntegrationEventTypeRegistry registry, IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly ActivitySource Source = new("ModularApi.Messaging");
    private static readonly Meter Meter = new("ModularApi.Messaging");
    private static readonly Counter<long> Processed = Meter.CreateCounter<long>("outbox.messages.processed");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("outbox.messages.failed");
    private static readonly Counter<long> Lost = Meter.CreateCounter<long>("outbox.lease.lost");
    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("outbox.delivery.latency", unit: "ms");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = options.Value;
        if (!cfg.Enabled) return;
        // Uma sequência por módulo; o limite global é adquirido antes do claim, nunca com lease em espera.
        using var capacity = new SemaphoreSlim(cfg.MaxConcurrentDeliveries);
        await Task.WhenAll(stores.Select(store => ProcessStoreAsync(store, cfg, capacity, stoppingToken)));
    }

    private async Task ProcessStoreAsync(IOutboxStore store, OutboxOptions cfg, SemaphoreSlim capacity, CancellationToken stoppingToken)
    {
        var nextCleanup = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            var didWork = false;
            try
            {
                for (var n = 0; n < cfg.BatchSize; n++)
                {
                    await capacity.WaitAsync(stoppingToken);
                    try
                    {
                        var batch = await store.ClaimBatchAsync(1, TimeSpan.FromSeconds(cfg.LockSeconds), cfg.MaxAttempts, stoppingToken);
                        if (batch.Count == 0) break;
                        didWork = true;
                        await Deliver(store, batch[0], cfg, stoppingToken);
                    }
                    finally { capacity.Release(); }
                }
                if (DateTimeOffset.UtcNow >= nextCleanup)
                {
                    await store.PruneProcessedAsync(cfg.ProcessedRetentionDays, stoppingToken);
                    nextCleanup = DateTimeOffset.UtcNow.AddHours(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                didWork = false; // Evita retry em loop quente quando o armazenamento falha.
                logger.LogError(ex, "Outbox {Module}: ciclo falhou", store.Module);
            }
            if (!didWork)
                try { await Task.Delay(cfg.PollingIntervalMs, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            else await Task.Yield();
        }
    }
    private async Task Deliver(IOutboxStore store, OutboxMessage message, OutboxOptions cfg, CancellationToken ct)
    {
        var parent = ActivityContext.TryParse(message.TraceParent, null, out var context) ? context : default;
        using var activity = Source.StartActivity("outbox.deliver", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.message.id", message.Id);
        activity?.SetTag("messaging.module", store.Module);
        var token = message.ClaimToken!.Value;
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(ct);
        processing.CancelAfter(TimeSpan.FromSeconds(cfg.HandlerTimeoutSeconds));
        using var renewalStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var renewal = Renew(store, message.Id, token, cfg, processing, renewalStop.Token);
        var tags = new TagList { { "module", store.Module }, { "event.type", registry.Resolve(message.Type)?.Name ?? "unknown" } };
        try
        {
            var type = registry.Resolve(message.Type) ?? throw new InvalidOperationException("UnknownEventContract");
            var integrationEvent = (IIntegrationEvent?)JsonSerializer.Deserialize(message.Payload, type, JsonOptions)
                ?? throw new InvalidOperationException("InvalidEventPayload");
            await publisher.PublishAsync(integrationEvent, processing.Token).WaitAsync(processing.Token);
            if (await store.MarkProcessedAsync(message.Id, token, ct))
            {
                Processed.Add(1, tags);
                Latency.Record((DateTimeOffset.UtcNow - message.OccurredOn).TotalMilliseconds, tags);
            }
            else Lost.Add(1, tags);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Failed.Add(1, tags);
            var code = ex is OperationCanceledException ? "DeliveryTimeoutOrLeaseLost" : ex.GetType().Name;
            activity?.SetStatus(ActivityStatusCode.Error, code);
            logger.LogWarning(ex, "Falha Outbox {Module} {MessageId}, tentativa {Attempt}, código {ErrorCode}", store.Module, message.Id, message.Attempts, code);
            var delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, message.Attempts)) + Random.Shared.NextDouble());
            await store.MarkFailedAsync(message.Id, token, code, delay, cfg.MaxAttempts, ct);
        }
        finally
        {
            await renewalStop.CancelAsync();
            await renewal;
        }
    }
    private async Task Renew(IOutboxStore store, Guid id, Guid token, OutboxOptions cfg, CancellationTokenSource processing, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(cfg.LockSeconds / 3d), ct);
                if (!await store.RenewAsync(id, token, TimeSpan.FromSeconds(cfg.LockSeconds), ct))
                {
                    await processing.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception)
        {
            await processing.CancelAsync();
            // Sem prova da renovação, não se assume propriedade. O ACK também é cercado pelo token.
        }
    }
}
