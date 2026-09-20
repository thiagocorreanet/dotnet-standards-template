using Module.Talks.Domain;

namespace Module.Talks.UseCases.AddContent;

public sealed record AddContentResponse(Guid Id, Guid TalkId, string ContentTitle, ContentType ContentType, string ContentUrl);
