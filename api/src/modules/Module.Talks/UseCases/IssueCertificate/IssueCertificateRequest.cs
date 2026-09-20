using System.Text.Json.Serialization;

namespace Module.Talks.UseCases.IssueCertificate;

public sealed record IssueCertificateRequest(Guid PersonId)
{
    [JsonIgnore]
    public Guid TalkId { get; init; }
}
