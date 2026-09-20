using Shared.Data.Entities;

namespace Module.Talks.Domain;

/// <summary>Registro de presença de uma pessoa em uma palestra. Única por (palestra, pessoa) entre ativos.</summary>
public sealed class Attendance : BaseEntity
{
    private Attendance()
    {
    }

    internal Attendance(Guid talkId, Guid personId, DateTimeOffset attendanceRecordedAt)
    {
        TalkId = talkId;
        PersonId = personId;
        AttendanceRecordedAt = attendanceRecordedAt;
    }

    public Guid TalkId { get; private set; }
    public Guid PersonId { get; private set; }
    public DateTimeOffset AttendanceRecordedAt { get; private set; }
}
