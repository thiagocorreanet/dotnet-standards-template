using Shared.Observability.Telemetry;

namespace Module.Venues.Shared;

internal static class VenuesTelemetry
{
    public static readonly ModuleTelemetry Instance = new(VenuesDbContext.SchemaName);
}
