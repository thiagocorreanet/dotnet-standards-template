using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Shared.Observability.Middleware;
using Shared.Observability.Telemetry;

namespace Shared.Observability;

/// <summary>
/// Observabilidade como pilar: logs estruturados (Serilog) correlacionados com traces (OpenTelemetry),
/// métricas de runtime/HTTP/Npgsql e de negócio (por módulo). Exporta por uma rota OTLP ao Collector;
/// o backend local ou gerenciado é configurado na infraestrutura.
/// </summary>
public static class ObservabilityExtensions
{
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        var version = typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        builder.Services.AddSerilog((services, configuration) =>
        {
            // Os níveis/overrides ficam no logger de entrada. Todos os sinks (configuração, DI e OTLP)
            // ficam atrás da sanitização; nenhum recebe o objeto Exception original.
            var levels = new ConfigurationBuilder().AddInMemoryCollection(builder.Configuration.AsEnumerable()
                .Where(p => p.Key == "Serilog:MinimumLevel" || p.Key.StartsWith("Serilog:MinimumLevel:", StringComparison.OrdinalIgnoreCase)))
                .Build();
            configuration
                .ReadFrom.Configuration(levels)
                .Enrich.FromLogContext()
                .Enrich.With<PrivacyLogEnricher>()
                .Filter.ByExcluding(e => e.Exception is null &&
                    e.Properties.TryGetValue("SourceContext", out var source) &&
                    source.ToString().Contains("Microsoft.EntityFrameworkCore.Database.Command", StringComparison.Ordinal))
                .Enrich.WithMachineName()
                .Enrich.WithProperty("EnvironmentName", builder.Environment.EnvironmentName)
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", serviceName)
                .Enrich.WithProperty("Version", version);

            var destinations = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services);
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                destinations.WriteTo.OpenTelemetry(o =>
                {
                    o.Endpoint = otlpEndpoint;
                    o.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = serviceName, ["service.version"] = version };
                });
            }
            configuration.WriteTo.Sink(new PrivacyLogSink(destinations.CreateLogger()));
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: version, serviceInstanceId: Environment.MachineName)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)]))
            .WithTracing(t => t
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(builder.Configuration.GetValue("Observability:TraceSampleRatio", 0.1))))
                .AddSource($"{ModuleTelemetry.Prefix}.*")
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.RecordException = false;
                    o.EnrichWithHttpResponse = (activity, response) =>
                    {
                        activity.SetTag("url.path", (response.HttpContext.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText ?? "unmatched");
                        activity.SetTag("url.query", null);
                    };
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")
                        && !ctx.Request.Path.StartsWithSegments("/scalar")
                        && !ctx.Request.Path.StartsWithSegments("/openapi");
                    o.EnrichWithHttpRequest = (activity, request) =>
                    {
                        if (request.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var cid))
                        {
                            activity.SetTag("correlation.id", cid);
                        }
                    };
                })
                .AddHttpClientInstrumentation()
                .AddProcessor(new PrivacyTraceProcessor())
                .AddNpgsql())
            .WithMetrics(m => m
                .AddMeter($"{ModuleTelemetry.Prefix}.*")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddNpgsqlInstrumentation());

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>Correlação e log por template de rota, ator técnico e trace, sem IP ou user-agent.</summary>
    public static IApplicationBuilder UseObservability(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(o =>
        {
            o.MessageTemplate = "HTTP {RequestMethod} {RouteTemplate} => {StatusCode} em {Elapsed:0.0} ms";
            o.GetLevel = (ctx, _, ex) => ex is not null || ctx.Response.StatusCode >= 500 ? LogEventLevel.Error
                : ctx.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                : LogEventLevel.Information;
            o.EnrichDiagnosticContext = (diag, ctx) =>
            {
                diag.Set("ActorId", ctx.User.FindFirst("app_user_id")?.Value);
                diag.Set("RouteTemplate", (ctx.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText ?? "unmatched");
                diag.Set("TraceId", Activity.Current?.TraceId.ToString());
                if (ctx.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var cid))
                {
                    diag.Set("CorrelationId", cid);
                }
            };
        });
        return app;
    }
}
