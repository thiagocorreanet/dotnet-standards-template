namespace Module.People.UseCases.CreatePerson;

/// <summary>Dados para cadastro de uma pessoa. O CPF pode vir com máscara; é armazenado apenas com dígitos.</summary>
public sealed record CreatePersonRequest(
    string PersonName,
    string PersonEmail,
    string? PersonPhone,
    string? PersonDocument,
    string? PersonCompany,
    string? PersonJobTitle,
    string? PersonShortBio,
    string? PersonPhotoUrl,
    Guid? UserId = null);
