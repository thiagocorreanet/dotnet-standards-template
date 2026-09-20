using Shared.Observability.Telemetry;

namespace Module.Talks.Shared;

internal static class TalksTelemetry
{
    public static readonly ModuleTelemetry Instance = new(TalksDbContext.SchemaName);
}
