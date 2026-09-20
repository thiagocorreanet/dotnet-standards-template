namespace Module.People.UseCases.GetPerson;

public sealed record GetPersonResponse(
    Guid Id,
    string PersonName,
    string PersonEmail,
    string? PersonPhone,
    string? PersonDocument,
    string? PersonCompany,
    string? PersonJobTitle,
    string? PersonShortBio,
    string? PersonPhotoUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
