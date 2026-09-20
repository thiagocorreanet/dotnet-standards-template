namespace Module.Talks.UseCases.RemoveSpeaker;

public sealed record RemoveSpeakerRequest(Guid TalkId, Guid PersonId);
