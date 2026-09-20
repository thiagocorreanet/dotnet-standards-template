using Serilog.Core;
using Serilog.Events;
namespace Shared.Observability;
/// <summary>Remove campos sensíveis conhecidos. Exceções recebem uma allowlist adicional no PrivacyLogSink.</summary>
public sealed class PrivacyLogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var name in new[] { "RequestPath", "QueryString", "ClientIp", "UserAgent", "Authorization", "Cookie", "Password", "Token" })
            logEvent.RemovePropertyIfPresent(name);
    }
}
