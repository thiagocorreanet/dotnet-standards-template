using Module.Talks.Domain;

namespace Module.Talks.UseCases.AddSpeaker;

public sealed record AddSpeakerResponse(Guid TalkId, Guid PersonId, SpeakerRole SpeakerRole);
