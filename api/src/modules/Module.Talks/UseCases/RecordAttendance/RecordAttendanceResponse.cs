namespace Module.Talks.UseCases.RecordAttendance;

public sealed record RecordAttendanceResponse(Guid Id, Guid TalkId, Guid PersonId, DateTimeOffset AttendanceRecordedAt);
