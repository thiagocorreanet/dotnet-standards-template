using System.Text.Json.Serialization;

namespace Module.Talks.UseCases.UpdateTalk;

public sealed record UpdateTalkRequest(
    Guid TrackId,
    Guid? RoomId,
    string TalkTitle,
    string? TalkDescription,
    DateTimeOffset TalkStart,
    DateTimeOffset TalkEnd)
{
    /// <summary>Preenchido pela rota; não faz parte do corpo.</summary>
    [JsonIgnore]
    public Guid TalkId { get; init; }
}
