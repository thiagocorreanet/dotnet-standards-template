namespace Module.Talks.UseCases.UpdateTalk;

public sealed record UpdateTalkResponse(Guid Id, string TalkTitle, DateTimeOffset? UpdatedAt);
