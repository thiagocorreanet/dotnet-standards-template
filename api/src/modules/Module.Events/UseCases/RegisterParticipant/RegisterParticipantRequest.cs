using System.Text.Json.Serialization;

namespace Module.Events.UseCases.RegisterParticipant;

public sealed record RegisterParticipantRequest(Guid PersonId)
{
    /// <summary>Preenchido pela rota; não faz parte do corpo.</summary>
    [JsonIgnore]
    public Guid EventId { get; init; }
}
