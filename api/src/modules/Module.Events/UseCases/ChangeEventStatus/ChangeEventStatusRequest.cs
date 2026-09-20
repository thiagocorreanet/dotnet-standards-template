using System.Text.Json.Serialization;
using Module.Events.Domain;

namespace Module.Events.UseCases.ChangeEventStatus;

/// <summary>Nova situação desejada. <c>Reason</c> é obrigatório apenas para <c>Canceled</c>.</summary>
public sealed record ChangeEventStatusRequest(EventStatus EventStatus, string? Reason)
{
    /// <summary>Preenchido pela rota; não faz parte do corpo.</summary>
    [JsonIgnore]
    public Guid EventId { get; init; }
}
