namespace Module.Talks.UseCases.CreateTalk;

public sealed record CreateTalkResponse(Guid Id, Guid EventId, string TalkTitle);
