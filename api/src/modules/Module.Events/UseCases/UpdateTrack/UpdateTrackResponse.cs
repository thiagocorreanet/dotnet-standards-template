namespace Module.Events.UseCases.UpdateTrack;
public sealed record UpdateTrackResponse(Guid Id, Guid EventId, string TrackName, string? TrackDescription, string? TrackColor, bool IsActive);
