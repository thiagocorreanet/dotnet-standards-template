using Shared.Data.Entities;

namespace Module.Talks.Domain;

/// <summary>Certificado de participação em uma palestra, validável publicamente pelo <see cref="CertificateCode"/>.</summary>
public sealed class Certificate : BaseEntity
{
    private Certificate()
    {
    }

    internal Certificate(Guid talkId, Guid personId, string certificateCode, DateTimeOffset certificateIssuedAt, int certificateDurationMinutes)
    {
        TalkId = talkId;
        PersonId = personId;
        CertificateCode = certificateCode;
        CertificateIssuedAt = certificateIssuedAt;
        CertificateDurationMinutes = certificateDurationMinutes;
    }

    public Guid TalkId { get; private set; }
    public string PersonNameSnapshot { get; private set; } = "";
    public string TalkTitleSnapshot { get; private set; } = "";
    public string EventNameSnapshot { get; private set; } = "";
    public DateTimeOffset TalkStartSnapshot { get; private set; }
    public void RecordSnapshot(string personName, string title, string eventName, DateTimeOffset start)
    {
        if (PersonNameSnapshot.Length != 0) throw new InvalidOperationException("Snapshot já registrado.");
        PersonNameSnapshot = personName;
        TalkTitleSnapshot = title;
        EventNameSnapshot = eventName;
        TalkStartSnapshot = start;
    }
    public Guid PersonId { get; private set; }
    public string CertificateCode { get; private set; } = string.Empty;
    public DateTimeOffset CertificateIssuedAt { get; private set; }
    public int CertificateDurationMinutes { get; private set; }
}
