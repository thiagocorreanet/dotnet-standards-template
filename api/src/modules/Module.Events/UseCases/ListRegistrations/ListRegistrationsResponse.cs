using Module.Events.Domain;

namespace Module.Events.UseCases.ListRegistrations;

public sealed record ListRegistrationsItemResponse(
    Guid Id,
    Guid PersonId,
    string PersonName,
    string PersonEmail,
    RegistrationStatus RegistrationStatus,
    DateTimeOffset RegistrationRegisteredAt);
