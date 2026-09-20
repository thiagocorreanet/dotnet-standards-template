using Module.Events.Domain;

namespace Module.Events.UseCases.UpdateEvent;

public sealed record UpdateEventResponse(Guid Id, string EventName, EventStatus EventStatus, DateTimeOffset? UpdatedAt);
