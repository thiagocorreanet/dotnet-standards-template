using System.Text.Json.Serialization;
namespace Module.Events.UseCases.AddTrack;
public sealed record AddTrackRequest(string TrackName, string? TrackDescription, string? TrackColor)
{
    [JsonIgnore] public Guid EventId { get; init; }
}
