using System.Text.Json.Serialization;

namespace Module.Venues.UseCases.UpdateVenue;

public sealed record UpdateVenueRequest(
    string VenueName,
    string? VenueDescription,
    string? AddressStreet,
    string? AddressNumber,
    string? AddressNeighborhood,
    string AddressCity,
    string AddressState,
    string? AddressPostalCode)
{
    /// <summary>Preenchido pela rota; não faz parte do corpo.</summary>
    [JsonIgnore]
    public Guid VenueId { get; init; }
}
