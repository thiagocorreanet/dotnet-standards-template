using Module.Talks.Domain;

namespace Module.Talks.UseCases.CreateTalk;

/// <summary>Dados para criação de uma palestra. Exige ao menos um palestrante.</summary>
public sealed record CreateTalkRequest(
    Guid EventId,
    Guid TrackId,
    Guid? RoomId,
    string TalkTitle,
    string? TalkDescription,
    DateTimeOffset TalkStart,
    DateTimeOffset TalkEnd,
    IReadOnlyList<CreateTalkSpeakerRequest> Speakers);

public sealed record CreateTalkSpeakerRequest(Guid PersonId, SpeakerRole SpeakerRole);
