using Module.Talks.Domain;

namespace Module.Talks.UseCases.GetTalk;

public sealed record GetTalkResponse(
    Guid Id,
    Guid EventId,
    string EventName,
    Guid TrackId,
    string TrackName,
    Guid? RoomId,
    string? RoomName,
    string TalkTitle,
    string? TalkDescription,
    DateTimeOffset TalkStart,
    DateTimeOffset TalkEnd,
    int TalkDurationMinutes,
    IReadOnlyList<GetTalkSpeakerResponse> Speakers,
    IReadOnlyList<GetTalkContentResponse> Contents,
    int AttendancesCount,
    int CertificatesCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record GetTalkSpeakerResponse(Guid PersonId, string PersonName, SpeakerRole SpeakerRole);

public sealed record GetTalkContentResponse(Guid Id, string ContentTitle, ContentType ContentType, string ContentUrl, string? ContentDescription);
