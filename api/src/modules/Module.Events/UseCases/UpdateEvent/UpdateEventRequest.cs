using System.Text.Json.Serialization;
using Module.Events.Domain;

namespace Module.Events.UseCases.UpdateEvent;

public sealed record UpdateEventRequest(
    string EventName,
    string? EventDescription,
    DateTimeOffset EventStartDate,
    DateTimeOffset EventEndDate,
    EventFormat EventFormat,
    Guid? VenueId,
    string? EventRemoteUrl,
    int? EventMaximumCapacity)
{
    /// <summary>Preenchido pela rota; não faz parte do corpo.</summary>
    [JsonIgnore]
    public Guid EventId { get; init; }
}
