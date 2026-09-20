using Module.Events.Domain;

namespace Tests.Unit.Events;

/// <summary>Fábrica de eventos para os testes: cria em qualquer situação percorrendo transições válidas.</summary>
internal static class EventFactory
{
    public static readonly Guid VenueId = Guid.NewGuid();
    public static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset End = new(2026, 10, 1, 18, 0, 0, TimeSpan.Zero);

    public static Event InPerson(int? capacity = null, Guid? venueId = null) =>
        Event.Create("DevConf", "Conferência", Start, End, EventFormat.InPerson, venueId ?? VenueId, null, capacity).Value;

    public static Event Remote(int? capacity = null) =>
        Event.Create("Webinar", null, Start, End, EventFormat.Remote, null, "https://meet.exemplo.com/abc", capacity).Value;

    public static Event Hybrid() =>
        Event.Create("Meetup", null, Start, End, EventFormat.Hybrid, VenueId, "https://meet.exemplo.com/abc", null).Value;

    public static Event WithStatus(EventStatus status, int? capacity = null)
    {
        var eventEntity = InPerson(capacity);
        switch (status)
        {
            case EventStatus.Draft:
                break;
            case EventStatus.Published:
                eventEntity.Publish(1);
                break;
            case EventStatus.InProgress:
                eventEntity.Publish(1);
                eventEntity.Start();
                break;
            case EventStatus.Closed:
                eventEntity.Publish(1);
                eventEntity.Close();
                break;
            case EventStatus.Canceled:
                eventEntity.Cancel("Sem quórum");
                break;
        }

        eventEntity.ClearEvents();
        return eventEntity;
    }
}
