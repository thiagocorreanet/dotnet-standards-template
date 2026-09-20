using System.Text.Json.Serialization;
using Module.Talks.Domain;

namespace Module.Talks.UseCases.AddSpeaker;

public sealed record AddSpeakerRequest(Guid PersonId, SpeakerRole SpeakerRole)
{
    [JsonIgnore]
    public Guid TalkId { get; init; }
}
