namespace Module.People.UseCases.CreatePerson;

public sealed record CreatePersonResponse(Guid Id, string PersonName, string PersonEmail);
