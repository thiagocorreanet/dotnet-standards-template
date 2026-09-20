namespace Module.Events.UseCases.ListTracks;
public sealed record ListTracksItemResponse(Guid Id, Guid EventId, string TrackName, string? TrackDescription, string? TrackColor, bool IsActive);
