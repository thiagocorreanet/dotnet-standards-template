using Module.Events.Domain;
using Shouldly;

namespace Tests.Unit.Events;

public sealed class EventFormatTests
{
    private static readonly Guid Venue = Guid.NewGuid();
    private const string Link = "https://meet.exemplo.com/room";

    [Theory]
    [InlineData(EventFormat.InPerson, true, null, true)]
    [InlineData(EventFormat.InPerson, true, Link, true)]
    [InlineData(EventFormat.InPerson, false, null, false)]
    [InlineData(EventFormat.InPerson, false, Link, false)]
    [InlineData(EventFormat.Remote, false, Link, true)]
    [InlineData(EventFormat.Remote, false, null, false)]
    [InlineData(EventFormat.Remote, false, "   ", false)]
    [InlineData(EventFormat.Remote, true, Link, false)]
    [InlineData(EventFormat.Hybrid, true, Link, true)]
    [InlineData(EventFormat.Hybrid, true, null, false)]
    [InlineData(EventFormat.Hybrid, false, Link, false)]
    [InlineData(EventFormat.Hybrid, false, null, false)]
    public void IsFormatConsistent_should_follow_the_matrix_of_local_and_link(EventFormat format, bool withVenue, string? link, bool expected)
    {
        Event.IsFormatConsistent(format, withVenue ? Venue : null, link).ShouldBe(expected);
    }

    [Fact]
    public void IsFormatConsistent_should_treat_Guid_empty_as_absence_of_local()
    {
        Event.IsFormatConsistent(EventFormat.InPerson, Guid.Empty, null).ShouldBeFalse();
    }

    [Fact]
    public void Create_in_person_without_local_should_fail_with_InconsistentFormat()
    {
        var result = Event.Create("X", null, EventFactory.Start, EventFactory.End, EventFormat.InPerson, null, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EventsErrors.InconsistentFormat);
    }

    [Fact]
    public void Create_remote_with_local_should_fail_with_InconsistentFormat()
    {
        var result = Event.Create("X", null, EventFactory.Start, EventFactory.End, EventFormat.Remote, Venue, Link, null);

        result.Error.ShouldBe(EventsErrors.InconsistentFormat);
    }

    [Fact]
    public void Create_hybrid_without_link_should_fail_with_InconsistentFormat()
    {
        var result = Event.Create("X", null, EventFactory.Start, EventFactory.End, EventFormat.Hybrid, Venue, "", null);

        result.Error.ShouldBe(EventsErrors.InconsistentFormat);
    }

    [Fact]
    public void Create_should_normalize_texts_and_store_data()
    {
        var result = Event.Create("  DevConf  ", "  ", EventFactory.Start, EventFactory.End, EventFormat.Hybrid, Venue, $"  {Link} ", 50);

        result.IsSuccess.ShouldBeTrue();
        var eventEntity = result.Value;
        eventEntity.EventName.ShouldBe("DevConf");
        eventEntity.EventDescription.ShouldBeNull();
        eventEntity.EventRemoteUrl.ShouldBe(Link);
        eventEntity.VenueId.ShouldBe(Venue);
        eventEntity.EventFormat.ShouldBe(EventFormat.Hybrid);
        eventEntity.EventMaximumCapacity.ShouldBe(50);
        eventEntity.EventStartDate.ShouldBe(EventFactory.Start);
        eventEntity.EventEndDate.ShouldBe(EventFactory.End);
    }

    [Fact]
    public void Update_with_format_inconsistent_should_fail_and_preserve_data()
    {
        var eventEntity = EventFactory.InPerson();

        var result = eventEntity.Update("Other", null, EventFactory.Start, EventFactory.End, EventFormat.Remote, Venue, Link, null);

        result.Error.ShouldBe(EventsErrors.InconsistentFormat);
        eventEntity.EventName.ShouldBe("DevConf");
        eventEntity.EventFormat.ShouldBe(EventFormat.InPerson);
    }

    [Fact]
    public void Update_of_in_person_to_remote_should_clear_local()
    {
        var eventEntity = EventFactory.InPerson();

        var result = eventEntity.Update("DevConf Online", null, EventFactory.Start, EventFactory.End, EventFormat.Remote, null, Link, null);

        result.IsSuccess.ShouldBeTrue();
        eventEntity.VenueId.ShouldBeNull();
        eventEntity.EventRemoteUrl.ShouldBe(Link);
        eventEntity.EventFormat.ShouldBe(EventFormat.Remote);
    }
}
