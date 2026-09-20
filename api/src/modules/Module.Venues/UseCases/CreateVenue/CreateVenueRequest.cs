namespace Module.Venues.UseCases.CreateVenue;

/// <summary>Dados para criação de um local. Informe <c>SingleRoomCapacity</c> quando o local tiver um único ambiente.</summary>
public sealed record CreateVenueRequest(
    string VenueName,
    string? VenueDescription,
    string? AddressStreet,
    string? AddressNumber,
    string? AddressNeighborhood,
    string AddressCity,
    string AddressState,
    string? AddressPostalCode,
    int? SingleRoomCapacity);
