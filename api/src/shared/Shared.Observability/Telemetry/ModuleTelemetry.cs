using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Shared.Observability.Telemetry;

/// <summary>
/// Fonte de traces e métricas por módulo. Convenção de nomes <c>ModularApi.{Module}</c>, capturados por wildcard na
/// configuração do OpenTelemetry. Uso: <c>using var atividade = VenuesTelemetry.Instance.StartActivity("CreateVenue");</c>
/// </summary>
public sealed class ModuleTelemetry
{
    public const string Prefix = "ModularApi";

    public ModuleTelemetry(string module)
    {
        Module = module;
        ActivitySource = new ActivitySource($"{Prefix}.{module}");
        Meter = new Meter($"{Prefix}.{module}");
        ExecutedUseCases = Meter.CreateCounter<long>("usecase.executions", description: "Casos de uso executados");
        FailedUseCases = Meter.CreateCounter<long>("usecase.failures", description: "Casos de uso com falha de negócio");
        UseCaseDuration = Meter.CreateHistogram<double>("usecase.duration", unit: "ms", description: "Duração dos casos de uso");
    }

    public string Module { get; }
    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }
    public Counter<long> ExecutedUseCases { get; }
    public Counter<long> FailedUseCases { get; }
    public Histogram<double> UseCaseDuration { get; }

    public Activity? StartActivity(string useCase) => ActivitySource.StartActivity($"{Module}.{useCase}", ActivityKind.Internal);

    public void RecordExecution(string useCase, bool success, double durationMs, string? codeError = null)
    {
        var tags = new TagList { { "usecase", useCase }, { "module", Module } };
        ExecutedUseCases.Add(1, tags);
        UseCaseDuration.Record(durationMs, tags);
        if (!success)
        {
            tags.Add("error.code", codeError);
            FailedUseCases.Add(1, tags);
        }
    }
}
