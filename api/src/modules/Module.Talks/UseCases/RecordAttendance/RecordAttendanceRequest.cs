using System.Text.Json.Serialization;

namespace Module.Talks.UseCases.RecordAttendance;

public sealed record RecordAttendanceRequest(Guid PersonId)
{
    [JsonIgnore]
    public Guid TalkId { get; init; }
}
