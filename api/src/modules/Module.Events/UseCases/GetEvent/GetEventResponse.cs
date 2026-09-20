using Module.Events.Domain;

namespace Module.Events.UseCases.GetEvent;

public sealed record GetEventResponse(
    Guid Id,
    string EventName,
    string? EventDescription,
    DateTimeOffset EventStartDate,
    DateTimeOffset EventEndDate,
    EventFormat EventFormat,
    EventStatus EventStatus,
    Guid? VenueId,
    string? VenueName,
    string? EventRemoteUrl,
    int? EventMaximumCapacity,
    int ConfirmedRegistrations,
    string? EventCancellationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<GetEventTrackResponse> Tracks);

public sealed record GetEventTrackResponse(Guid Id, string TrackName, string? TrackDescription, string? TrackColor, bool IsActive);
