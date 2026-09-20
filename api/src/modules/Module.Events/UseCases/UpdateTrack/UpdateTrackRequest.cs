using System.Text.Json.Serialization;
namespace Module.Events.UseCases.UpdateTrack;
public sealed record UpdateTrackRequest(string TrackName, string? TrackDescription, string? TrackColor, bool IsActive)
{
    [JsonIgnore] public Guid EventId { get; init; }
    [JsonIgnore] public Guid TrackId { get; init; }
}
