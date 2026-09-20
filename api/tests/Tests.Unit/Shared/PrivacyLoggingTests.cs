using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Shared.Observability;

namespace Tests.Unit.Shared;

public sealed class PrivacyLoggingTests
{
    [Fact]
    public void Exceptions_reach_every_sink_sanitized_with_correlation_and_stable_fingerprint()
    {
        var first = new CaptureSink();
        var second = new CaptureSink();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "";
        builder.AddObservability("LoggingTest");
        builder.Services.AddSingleton<ILogEventSink>(first);
        builder.Services.AddSingleton<ILogEventSink>(second);
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Regression.Worker");
        using var activity = new Activity("failure-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = "probe-123", ["Payload"] = "private-scope-value" });

        foreach (var secret in new[] { "private-first", "private-second" })
        {
            var exception = CreateException(secret);
            exception.Data["Authorization"] = "private-token-value";
            logger.LogError(exception, "Interpolated private-template-value {Password} {@Payload}", secret, new { Email = "private@example.test" });
        }

        foreach (var sink in new[] { first, second })
        {
            sink.Events.Count.ShouldBe(2);
            var events = sink.Events.ToArray();
            foreach (var item in events)
            {
                item.Exception.ShouldBeNull();
                item.Level.ShouldBe(LogEventLevel.Error);
                item.Properties["ExceptionType"].ToString().ShouldContain(nameof(InvalidOperationException));
                item.Properties["SourceContext"].ToString().ShouldContain("Regression.Worker");
                item.Properties["CorrelationId"].ToString().ShouldContain("probe-123");
                item.TraceId.ShouldBe(activity.TraceId);
                item.SpanId.ShouldBe(activity.SpanId);
                (item.RenderMessage() + string.Join('|', item.Properties.Select(x => x.ToString())))
                    .ShouldNotContain("private");
            }
            events[0].Properties["ErrorFingerprint"].ToString().ShouldBe(events[1].Properties["ErrorFingerprint"].ToString());
        }
    }

    [Fact]
    public void Normal_logs_keep_their_template_and_levels_but_database_payload_is_suppressed()
    {
        var sink = new CaptureSink();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "";
        builder.Configuration["Serilog:MinimumLevel:Default"] = "Debug";
        builder.Configuration["Serilog:MinimumLevel:Override:Microsoft"] = "Warning";
        builder.AddObservability("LoggingTest");
        builder.Services.AddSingleton<ILogEventSink>(sink);
        using var host = builder.Build();
        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        factory.CreateLogger("Regression.Normal").LogDebug("Normal {Operation} {Password}", "read", "private-password");
        factory.CreateLogger("Microsoft.Sample").LogInformation("MustNotAppear");
        var databaseLogger = factory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");
        databaseLogger.LogWarning("SQL private-query-value");
        databaseLogger.LogError(new InvalidOperationException("private-db-error"), "SQL private-query-value");

        sink.Events.Count.ShouldBe(2);
        var normal = sink.Events.Single(x => x.Exception is null && x.Level == LogEventLevel.Debug);
        normal.MessageTemplate.Text.ShouldBe("Normal {Operation} {Password}");
        normal.Properties.ShouldNotContainKey("Password");
        normal.Properties["Operation"].ToString().ShouldContain("read");
        var failure = sink.Events.Single(x => x.Level == LogEventLevel.Error);
        failure.Properties.ShouldContainKey("ErrorFingerprint");
        failure.RenderMessage().ShouldNotContain("private");
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    private static InvalidOperationException CreateException(string secret)
    {
        try { throw new InvalidOperationException(secret, new ArgumentException("private-inner-value")); }
        catch (InvalidOperationException exception) { return exception; }
    }
}
