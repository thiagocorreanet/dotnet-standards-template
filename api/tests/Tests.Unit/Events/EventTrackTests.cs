using Module.Events.Domain;
using Shouldly;

namespace Tests.Unit.Events;

public sealed class EventTrackTests
{
    private static Event NewEvent() => Event.Create("Event", null, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2), EventFormat.Remote, null, "https://eventEntity.test", null).Value;

    [Fact]
    public void Should_add_update_and_normalize_track()
    {
        var eventEntity = NewEvent();
        var created = eventEntity.AddTrack("  Backend  ", "  APIs e dados  ", "#aabbcc");
        created.IsSuccess.ShouldBeTrue();
        created.Value.TrackName.ShouldBe("Backend");
        created.Value.TrackColor.ShouldBe("#AABBCC");
        eventEntity.UpdateTrack(created.Value.Id, "Arquitetura", null, "#112233").IsSuccess.ShouldBeTrue();
        created.Value.TrackName.ShouldBe("Arquitetura");
    }

    [Fact]
    public void Names_repeated_should_be_rejected_without_distinguish_case()
    {
        var eventEntity = NewEvent(); eventEntity.AddTrack("Cloud", null, null);
        eventEntity.AddTrack(" cloud ", null, null).Error.ShouldBe(EventsErrors.DuplicateTrackName);
    }

    [Fact]
    public void Event_should_preserve_to_least_a_track()
    {
        var eventEntity = NewEvent(); var first = eventEntity.AddTrack("Única", null, null).Value;
        eventEntity.RemoveTrack(first.Id).Error.ShouldBe(EventsErrors.EventRequiresTrack);
        var second = eventEntity.AddTrack("Outra", null, null).Value;
        eventEntity.RemoveTrack(second.Id).IsSuccess.ShouldBeTrue();
    }
}
