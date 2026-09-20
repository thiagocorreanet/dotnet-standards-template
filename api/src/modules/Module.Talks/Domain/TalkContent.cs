using Shared.Data.Entities;

namespace Module.Talks.Domain;

/// <summary>Material da palestra: slides, PDF, link, vídeo etc.</summary>
public sealed class TalkContent : BaseEntity
{
    private TalkContent()
    {
    }

    internal TalkContent(Guid talkId, string contentTitle, ContentType contentType, string contentUrl, string? contentDescription)
    {
        TalkId = talkId;
        ContentTitle = contentTitle.Trim();
        ContentType = contentType;
        ContentUrl = contentUrl.Trim();
        ContentDescription = contentDescription?.Trim();
    }

    public Guid TalkId { get; private set; }
    public string ContentTitle { get; private set; } = string.Empty;
    public ContentType ContentType { get; private set; }
    public string ContentUrl { get; private set; } = string.Empty;
    public string? ContentDescription { get; private set; }
}
