namespace Module.People.UseCases.UpdatePerson;

public sealed record UpdatePersonResponse(Guid Id, string PersonName, string PersonEmail, bool IsActive, DateTimeOffset? UpdatedAt);
