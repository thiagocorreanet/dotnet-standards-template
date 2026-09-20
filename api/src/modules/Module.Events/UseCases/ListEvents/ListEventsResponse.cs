using Module.Events.Domain;

namespace Module.Events.UseCases.ListEvents;

public sealed record ListEventsItemResponse(
    Guid Id,
    string EventName,
    DateTimeOffset EventStartDate,
    DateTimeOffset EventEndDate,
    EventFormat EventFormat,
    EventStatus EventStatus,
    Guid? VenueId,
    int ConfirmedRegistrations);
