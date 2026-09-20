namespace Module.Talks.UseCases.RemoveSpeaker;

public sealed record RemoveSpeakerResponse(Guid TalkId, Guid PersonId);
