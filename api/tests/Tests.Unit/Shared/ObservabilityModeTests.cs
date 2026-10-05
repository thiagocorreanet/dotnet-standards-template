using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Observability;
using Shouldly;

namespace Tests.Unit.Shared;

/// <summary>Modo lite: sem endpoint OTLP, a API não registra exportação e os logs seguem só nos sinks configurados.</summary>
public sealed class ObservabilityModeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Empty_endpoint_registers_no_otlp_exporter(string? endpoint) =>
        OtlpRegistrations(endpoint).ShouldBeEmpty();

    [Fact]
    public void Configured_endpoint_registers_the_otlp_exporter() =>
        OtlpRegistrations("http://collector:4317").ShouldNotBeEmpty();

    private static string[] OtlpRegistrations(string? endpoint)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development" });
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
        builder.AddObservability("ModeTest");
        return builder.Services
            .Select(d => d.ServiceType.FullName + "|" + d.ImplementationType?.FullName)
            .Where(name => name.Contains("OpenTelemetry.Exporter", StringComparison.Ordinal) || name.Contains("Otlp", StringComparison.Ordinal))
            .ToArray();
    }
}
