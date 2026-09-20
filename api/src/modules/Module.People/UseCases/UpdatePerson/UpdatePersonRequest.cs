using System.Text.Json.Serialization;

namespace Module.People.UseCases.UpdatePerson;

/// <summary>Atualização completa da pessoa (PUT): todos os campos são substituídos, inclusive <c>IsActive</c>.</summary>
public sealed record UpdatePersonRequest(
    string PersonName,
    string PersonEmail,
    string? PersonPhone,
    string? PersonDocument,
    string? PersonCompany,
    string? PersonJobTitle,
    string? PersonShortBio,
    string? PersonPhotoUrl,
    bool IsActive = true)
{
    /// <summary>Preenchido pela rota; não faz parte do corpo.</summary>
    [JsonIgnore]
    public Guid PersonId { get; init; }
}
