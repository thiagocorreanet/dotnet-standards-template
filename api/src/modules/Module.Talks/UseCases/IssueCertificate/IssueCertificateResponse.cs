using System.Text.Json.Serialization;

namespace Module.Talks.UseCases.IssueCertificate;

public sealed record IssueCertificateResponse(
    Guid Id,
    string CertificateCode,
    Guid TalkId,
    string TalkTitle,
    Guid PersonId,
    string PersonName,
    DateTimeOffset CertificateIssuedAt,
    int CertificateDurationMinutes)
{
    /// <summary>Indica se o certificado foi criado nesta chamada (201) ou já existia (200). Não é serializado.</summary>
    [JsonIgnore]
    public bool Created { get; init; }
}
