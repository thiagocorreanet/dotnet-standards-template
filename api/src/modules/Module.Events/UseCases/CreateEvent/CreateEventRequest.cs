using Module.Events.Domain;

namespace Module.Events.UseCases.CreateEvent;

/// <summary>Dados para criação de um evento. O evento nasce em <c>Draft</c>.</summary>
public sealed record CreateEventRequest(
    string EventName,
    string? EventDescription,
    DateTimeOffset EventStartDate,
    DateTimeOffset EventEndDate,
    EventFormat EventFormat,
    Guid? VenueId,
    string? EventRemoteUrl,
    int? EventMaximumCapacity,
    IReadOnlyList<CreateEventTrackRequest>? Tracks = null);

public sealed record CreateEventTrackRequest(string TrackName, string? TrackDescription, string? TrackColor);
