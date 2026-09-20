using Module.Events.Domain;

namespace Module.Events.UseCases.CreateEvent;

public sealed record CreateEventResponse(Guid Id, string EventName, EventStatus EventStatus);
