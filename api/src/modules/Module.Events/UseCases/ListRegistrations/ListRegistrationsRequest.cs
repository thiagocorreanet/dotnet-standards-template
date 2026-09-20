using System.Text.Json.Serialization;
using Module.Events.Domain;

namespace Module.Events.UseCases.ListRegistrations;

/// <summary>Filtros de listagem de inscrições de um evento (query string).</summary>
public sealed record ListRegistrationsRequest(RegistrationStatus? RegistrationStatus, int Page = 1, int PageSize = 20)
{
    /// <summary>Preenchido pela rota; não faz parte da query string.</summary>
    [JsonIgnore]
    public Guid EventId { get; init; }
}
