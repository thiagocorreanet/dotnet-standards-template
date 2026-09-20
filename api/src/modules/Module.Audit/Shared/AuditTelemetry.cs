using Shared.Observability.Telemetry;

namespace Module.Audit.Shared;

internal static class AuditTelemetry
{
    public static readonly ModuleTelemetry Instance = new(AuditDbContext.SchemaName);
}
