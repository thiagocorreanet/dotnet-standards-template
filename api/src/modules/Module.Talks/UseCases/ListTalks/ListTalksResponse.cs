namespace Module.Talks.UseCases.ListTalks;

public sealed record ListTalksItemResponse(
    Guid Id,
    Guid EventId,
    Guid TrackId,
    Guid? RoomId,
    string TalkTitle,
    DateTimeOffset TalkStart,
    DateTimeOffset TalkEnd,
    int SpeakersCount,
    int AttendancesCount);
