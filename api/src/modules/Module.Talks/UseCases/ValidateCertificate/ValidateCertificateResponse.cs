namespace Module.Talks.UseCases.ValidateCertificate;

public sealed record ValidateCertificateResponse(
    string CertificateCode,
    string TalkTitle,
    string EventName,
    string PersonName,
    DateTimeOffset TalkStart,
    DateTimeOffset CertificateIssuedAt,
    int CertificateDurationMinutes);
