namespace Module.Talks.UseCases.ListAttendances;

public sealed record ListAttendancesItemResponse(Guid Id, Guid PersonId, string PersonName, DateTimeOffset AttendanceRecordedAt, bool CertificateIssued);
