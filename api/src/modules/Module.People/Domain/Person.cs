using Shared.Contracts.People;
using Shared.Data.Entities;

namespace Module.People.Domain;

/// <summary>
/// Agregado Pessoa: palestrantes e participantes são Pessoas; o papel nasce do relacionamento com eventos e palestras.
/// E-mail é armazenado em minúsculas e CPF apenas com dígitos; ambos são únicos entre pessoas ativas (não excluídas).
/// </summary>
public sealed class Person : BaseEntity
{
    public Guid? UserId { get; private set; }
    public void LinkUser(Guid? userId) => UserId = userId;

    private Person()
    {
    }

    public string PersonName { get; private set; } = string.Empty;
    public string PersonEmail { get; private set; } = string.Empty;
    public string? PersonPhone { get; private set; }

    /// <summary>CPF somente dígitos (11 caracteres), sem máscara.</summary>
    public string? PersonDocument { get; private set; }
    public string? PersonCompany { get; private set; }
    public string? PersonJobTitle { get; private set; }
    public string? PersonShortBio { get; private set; }
    public string? PersonPhotoUrl { get; private set; }

    public static Person Create(
        string personName,
        string personEmail,
        string? personPhone,
        string? personDocument,
        string? personCompany,
        string? personJobTitle,
        string? personShortBio,
        string? personPhotoUrl)
    {
        var person = new Person();
        person.Update(personName, personEmail, personPhone, personDocument, personCompany, personJobTitle, personShortBio, personPhotoUrl, isActive: true);
        person.RecordEvent(new PersonCreated(person.Id));
        return person;
    }

    public void Update(
        string personName,
        string personEmail,
        string? personPhone,
        string? personDocument,
        string? personCompany,
        string? personJobTitle,
        string? personShortBio,
        string? personPhotoUrl,
        bool isActive)
    {
        PersonName = personName.Trim();
        PersonEmail = NormalizeEmail(personEmail);
        PersonPhone = Clear(personPhone);
        PersonDocument = Cpf.Normalize(personDocument);
        PersonCompany = Clear(personCompany);
        PersonJobTitle = Clear(personJobTitle);
        PersonShortBio = Clear(personShortBio);
        PersonPhotoUrl = Clear(personPhotoUrl);
        IsActive = isActive;
    }

    public void MarkDeleted() => RecordEvent(new PersonDeleted(Id));

    /// <summary>E-mail é comparado sem distinção de maiúsculas: sempre armazenado em minúsculas e sem espaços nas pontas.</summary>
    public static string NormalizeEmail(string personEmail) => personEmail.Trim().ToLowerInvariant();

    private static string? Clear(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
