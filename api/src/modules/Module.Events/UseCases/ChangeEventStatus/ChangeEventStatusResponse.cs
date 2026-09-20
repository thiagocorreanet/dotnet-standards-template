using Module.Events.Domain;

namespace Module.Events.UseCases.ChangeEventStatus;

public sealed record ChangeEventStatusResponse(Guid Id, EventStatus EventStatus);
