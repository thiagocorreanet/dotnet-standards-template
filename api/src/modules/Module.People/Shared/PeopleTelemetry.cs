using Shared.Observability.Telemetry;

namespace Module.People.Shared;

internal static class PeopleTelemetry
{
    public static readonly ModuleTelemetry Instance = new(PeopleDbContext.SchemaName);
}
