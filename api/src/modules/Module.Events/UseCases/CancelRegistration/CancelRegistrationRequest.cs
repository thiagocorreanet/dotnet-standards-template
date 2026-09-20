namespace Module.Events.UseCases.CancelRegistration;

public sealed record CancelRegistrationRequest(Guid EventId, Guid RegistrationId);
