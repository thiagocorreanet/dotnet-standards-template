namespace Module.Events.UseCases.AddTrack;
public sealed record AddTrackResponse(Guid Id, Guid EventId, string TrackName, string? TrackDescription, string? TrackColor);
