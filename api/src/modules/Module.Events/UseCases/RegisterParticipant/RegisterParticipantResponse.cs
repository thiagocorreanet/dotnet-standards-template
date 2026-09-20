using Module.Events.Domain;

namespace Module.Events.UseCases.RegisterParticipant;

public sealed record RegisterParticipantResponse(Guid Id, Guid EventId, Guid PersonId, RegistrationStatus RegistrationStatus, DateTimeOffset RegistrationRegisteredAt);
