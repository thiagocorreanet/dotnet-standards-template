using Shared.Observability.Telemetry;

namespace Module.Events.Shared;

internal static class EventsTelemetry
{
    public static readonly ModuleTelemetry Instance = new(EventsDbContext.SchemaName);
}
