namespace Module.People.UseCases.ListPeople;

public sealed record ListPeopleItemResponse(Guid Id, string PersonName, string PersonEmail, string? PersonCompany, string? PersonJobTitle, bool IsActive);
