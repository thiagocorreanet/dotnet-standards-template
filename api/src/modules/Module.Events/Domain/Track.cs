using Shared.Data.Entities;

namespace Module.Events.Domain;

/// <summary>Trilha temática que organiza as palestras de um evento.</summary>
public sealed class Track : BaseEntity
{
    private Track() { }

    internal Track(Guid eventId, string trackName, string? trackDescription, string? trackColor)
    {
        EventId = eventId;
        Update(trackName, trackDescription, trackColor);
    }

    public Guid EventId { get; private set; }
    public string TrackName { get; private set; } = string.Empty;
    public string? TrackDescription { get; private set; }
    public string? TrackColor { get; private set; }
    public Event Event { get; private set; } = null!;

    internal void Update(string trackName, string? trackDescription, string? trackColor)
    {
        TrackName = trackName.Trim();
        TrackDescription = string.IsNullOrWhiteSpace(trackDescription) ? null : trackDescription.Trim();
        TrackColor = string.IsNullOrWhiteSpace(trackColor) ? null : trackColor.Trim().ToUpperInvariant();
    }
}
