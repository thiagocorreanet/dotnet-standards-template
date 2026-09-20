using Shared.Contracts.Talks;
using Shared.Data.Entities;
using Shared.Http.Results;

namespace Module.Talks.Domain;

/// <summary>Palestrante informado na criação da palestra.</summary>
public sealed record NewSpeaker(Guid PersonId, SpeakerRole SpeakerRole);

/// <summary>
/// Agregado Palestra: sessão de um evento (módulo Eventos), opcionalmente alocada em uma sala (módulo Locais),
/// com palestrantes (Pessoas), conteúdos, presenças e certificados.
/// </summary>
public sealed class Talk : BaseEntity
{
    private readonly List<TalkSpeaker> _speakers = [];
    private readonly List<TalkContent> _contents = [];
    private readonly List<Attendance> _attendances = [];
    private readonly List<Certificate> _certificates = [];

    private Talk()
    {
    }

    public Guid EventId { get; private set; }
    public Guid TrackId { get; private set; }
    public Guid? RoomId { get; private set; }
    public string TalkTitle { get; private set; } = string.Empty;
    public string? TalkDescription { get; private set; }
    public DateTimeOffset TalkStart { get; private set; }
    public DateTimeOffset TalkEnd { get; private set; }

    /// <summary>Calculado, não persistido.</summary>
    public int TalkDurationMinutes => (int)(TalkEnd - TalkStart).TotalMinutes;

    public IReadOnlyCollection<TalkSpeaker> Speakers => _speakers.AsReadOnly();
    public IReadOnlyCollection<TalkContent> Contents => _contents.AsReadOnly();
    public IReadOnlyCollection<Attendance> Attendances => _attendances.AsReadOnly();
    public IReadOnlyCollection<Certificate> Certificates => _certificates.AsReadOnly();

    /// <summary>Cria a palestra com ao menos um palestrante; pessoas repetidas resultam em <see cref="TalksErrors.SpeakerAlreadyLinked"/>.</summary>
    public static Result<Talk> Create(
        Guid eventId,
        Guid trackId,
        Guid? roomId,
        string talkTitle,
        string? talkDescription,
        DateTimeOffset talkStart,
        DateTimeOffset talkEnd,
        IReadOnlyCollection<NewSpeaker> speakers)
    {
        if (speakers.Count == 0)
        {
            return TalksErrors.TalkRequiresSpeaker;
        }

        var talk = new Talk { EventId = eventId };
        talk.Update(trackId, roomId, talkTitle, talkDescription, talkStart, talkEnd);

        foreach (var speaker in speakers)
        {
            var result = talk.AddSpeaker(speaker.PersonId, speaker.SpeakerRole);
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        talk.RecordEvent(new TalkCreated(talk.Id, talk.EventId));
        return talk;
    }

    public void Update(Guid trackId, Guid? roomId, string talkTitle, string? talkDescription, DateTimeOffset talkStart, DateTimeOffset talkEnd)
    {
        TrackId = trackId;
        RoomId = roomId;
        TalkTitle = talkTitle.Trim();
        TalkDescription = talkDescription?.Trim();
        TalkStart = talkStart;
        TalkEnd = talkEnd;
    }

    public Result<TalkSpeaker> AddSpeaker(Guid personId, SpeakerRole speakerRole)
    {
        if (_speakers.Any(p => p.DeletedAt is null && p.PersonId == personId))
        {
            return TalksErrors.SpeakerAlreadyLinked;
        }

        var speaker = new TalkSpeaker(Id, personId, speakerRole);
        _speakers.Add(speaker);
        return speaker;
    }

    /// <summary>Retorna o vínculo a ser removido (soft delete feito pelo contexto). Uma palestra nunca fica sem palestrante.</summary>
    public Result<TalkSpeaker> RemoveSpeaker(Guid personId)
    {
        var speaker = _speakers.FirstOrDefault(p => p.DeletedAt is null && p.PersonId == personId);
        if (speaker is null)
        {
            return TalksErrors.SpeakerNotFound;
        }

        if (_speakers.Count(p => p.DeletedAt is null) <= 1)
        {
            return TalksErrors.TalkRequiresSpeaker;
        }

        return speaker;
    }

    public TalkContent AddContent(string contentTitle, ContentType contentType, string contentUrl, string? contentDescription)
    {
        var content = new TalkContent(Id, contentTitle, contentType, contentUrl, contentDescription);
        _contents.Add(content);
        return content;
    }

    /// <summary>Retorna o conteúdo a ser removido (soft delete feito pelo contexto).</summary>
    public Result<TalkContent> RemoveContent(Guid contentId)
    {
        var content = _contents.FirstOrDefault(c => c.DeletedAt is null && c.Id == contentId);
        return content is null ? TalksErrors.ContentNotFound : content;
    }

    /// <summary>Registra a presença (única por pessoa). A inscrição confirmada no evento é verificada pelo caso de uso via contrato.</summary>
    public Result<Attendance> RecordAttendance(Guid personId, DateTimeOffset now)
    {
        if (_attendances.Any(p => p.DeletedAt is null && p.PersonId == personId))
        {
            return TalksErrors.AttendanceAlreadyRecorded;
        }

        var attendance = new Attendance(Id, personId, now);
        _attendances.Add(attendance);
        RecordEvent(new AttendanceRecorded(Id, EventId, personId));
        return attendance;
    }

    /// <summary>
    /// Emite o certificado da pessoa: exige presença registrada e palestra encerrada (<c>TalkEnd &lt;= now</c>).
    /// Idempotente: se já existir, devolve o existente com <c>Created = false</c>.
    /// </summary>
    public Result<IssuanceCertificate> IssueCertificate(Guid personId, DateTimeOffset now)
    {
        var existing = _certificates.FirstOrDefault(c => c.DeletedAt is null && c.PersonId == personId);
        if (existing is not null)
        {
            return new IssuanceCertificate(existing, Created: false);
        }

        if (!_attendances.Any(p => p.DeletedAt is null && p.PersonId == personId))
        {
            return TalksErrors.AttendanceNotRecorded;
        }

        if (TalkEnd > now)
        {
            return TalksErrors.TalkNotEnded;
        }

        var certificate = new Certificate(Id, personId, CertificateCodeGenerator.Generate(), now, TalkDurationMinutes);
        _certificates.Add(certificate);
        RecordEvent(new CertificateIssued(certificate.Id, Id, personId));
        return new IssuanceCertificate(certificate, Created: true);
    }
}

/// <summary>Resultado da emissão: o certificado e se foi criado nesta chamada (201) ou já existia (200).</summary>
public sealed record IssuanceCertificate(Certificate Certificate, bool Created);
