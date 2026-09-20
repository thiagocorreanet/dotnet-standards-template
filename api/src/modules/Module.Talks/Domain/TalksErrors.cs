using Shared.Http.Results;

namespace Module.Talks.Domain;

public static class TalksErrors
{
    public static readonly Error TalkNotFound = Error.NotFound("Talks.TalkNotFound", "Palestra não encontrada.");
    public static readonly Error EventNotFound = Error.BusinessRule("Talks.EventNotFound", "O evento informado não existe.");
    public static readonly Error EventDoesNotAcceptTalks = Error.BusinessRule("Talks.EventDoesNotAcceptTalks", "O evento está encerrado ou cancelado e não aceita palestras.");
    public static readonly Error PeriodOutsideEvent = Error.BusinessRule("Talks.PeriodOutsideEvent", "O período da palestra deve estar dentro do período do evento.");
    public static readonly Error RoomNotFound = Error.BusinessRule("Talks.RoomNotFound", "A sala informada não existe ou está inativa.");
    public static readonly Error RoomDoesNotBelongToVenue = Error.BusinessRule("Talks.RoomDoesNotBelongToVenue", "A sala informada não pertence ao local do evento.");
    public static readonly Error RoomOccupied = Error.Conflict("Talks.RoomOccupied", "Já existe outra palestra nesta sala no horário informado.");
    public static readonly Error TrackNotFound = Error.BusinessRule("Talks.TrackNotFound", "A trilha informada não pertence ao evento ou está inativa.");
    public static readonly Error PersonNotFound = Error.BusinessRule("Talks.PersonNotFound", "Uma ou mais pessoas informadas como palestrantes não existem.");
    public static readonly Error SpeakerAlreadyLinked = Error.Conflict("Talks.SpeakerAlreadyLinked", "Esta pessoa já é palestrante desta palestra.");
    public static readonly Error SpeakerNotFound = Error.NotFound("Talks.SpeakerNotFound", "Palestrante não encontrado nesta palestra.");
    public static readonly Error TalkRequiresSpeaker = Error.BusinessRule("Talks.TalkRequiresSpeaker", "Uma palestra precisa manter ao menos um palestrante.");
    public static readonly Error ContentNotFound = Error.NotFound("Talks.ContentNotFound", "Conteúdo não encontrado nesta palestra.");
    public static readonly Error ParticipantNotRegistered = Error.BusinessRule("Talks.ParticipantNotRegistered", "A pessoa não possui inscrição confirmada no evento desta palestra.");
    public static readonly Error AttendanceAlreadyRecorded = Error.Conflict("Talks.AttendanceAlreadyRecorded", "A presença desta pessoa já foi registrada nesta palestra.");
    public static readonly Error AttendanceNotRecorded = Error.BusinessRule("Talks.AttendanceNotRecorded", "A pessoa não possui presença registrada nesta palestra.");
    public static readonly Error TalkNotEnded = Error.BusinessRule("Talks.TalkNotEnded", "O certificado só pode ser emitido após o término da palestra.");
    public static readonly Error CertificateNotFound = Error.NotFound("Talks.CertificateNotFound", "Certificado não encontrado.");
}
