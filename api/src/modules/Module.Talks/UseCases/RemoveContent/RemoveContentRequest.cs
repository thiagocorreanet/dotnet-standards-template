namespace Module.Talks.UseCases.RemoveContent;

public sealed record RemoveContentRequest(Guid TalkId, Guid ContentId);
