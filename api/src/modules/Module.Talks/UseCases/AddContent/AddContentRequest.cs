using System.Text.Json.Serialization;
using Module.Talks.Domain;

namespace Module.Talks.UseCases.AddContent;

public sealed record AddContentRequest(string ContentTitle, ContentType ContentType, string ContentUrl, string? ContentDescription)
{
    [JsonIgnore]
    public Guid TalkId { get; init; }
}
