using Shared.Observability.Telemetry;

namespace Module.Identity.Shared;

internal static class IdentityTelemetry
{
    public static readonly ModuleTelemetry Instance = new(IdentityDbContext.SchemaName);
}
