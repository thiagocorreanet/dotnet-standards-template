using Shared.Observability.Telemetry;

namespace Module.ModuleName.Shared;

internal static class ModuleNameTelemetry
{
    public static readonly ModuleTelemetry Instance = new(ModuleNameDbContext.SchemaName);
}
