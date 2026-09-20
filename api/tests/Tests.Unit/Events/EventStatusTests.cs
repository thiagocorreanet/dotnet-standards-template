using Module.Events.Domain;
using NSubstitute;
using Shared.Contracts.Events;
using Shared.Contracts.Talks;
using Shouldly;

namespace Tests.Unit.Events;

public sealed class EventStatusTests
{
    [Fact]
    public void Create_should_start_in_draft_without_events_of_integration()
    {
        var eventEntity = EventFactory.InPerson();

        eventEntity.EventStatus.ShouldBe(EventStatus.Draft);
        eventEntity.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Publish_in_draft_with_talk_should_publish_and_issue_EventPublished()
    {
        var eventEntity = EventFactory.InPerson();

        var result = eventEntity.Publish(talkCount: 1);

        result.IsSuccess.ShouldBeTrue();
        eventEntity.EventStatus.ShouldBe(EventStatus.Published);
        var integration = eventEntity.Events.ShouldHaveSingleItem().ShouldBeOfType<EventPublished>();
        integration.EventId.ShouldBe(eventEntity.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Publish_without_talks_should_fail_with_EventWithoutTalks(int talkCount)
    {
        var eventEntity = EventFactory.InPerson();

        var result = eventEntity.Publish(talkCount);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EventsErrors.EventWithoutTalks);
        eventEntity.EventStatus.ShouldBe(EventStatus.Draft);
        eventEntity.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(EventStatus.Published)]
    [InlineData(EventStatus.InProgress)]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Canceled)]
    public void Publish_outside_of_draft_should_fail_with_InvalidStatusTransition(EventStatus current)
    {
        var eventEntity = EventFactory.WithStatus(current);

        var result = eventEntity.Publish(5);

        result.Error.ShouldBe(EventsErrors.InvalidStatusTransition);
        eventEntity.EventStatus.ShouldBe(current);
    }

    [Fact]
    public async Task Publish_should_use_the_count_of_talks_of_contract_of_module_Talks()
    {
        var eventEntity = EventFactory.InPerson();
        var talks = Substitute.For<ITalksModuleApi>();
        talks.CountEventTalksAsync(eventEntity.Id, Arg.Any<CancellationToken>()).Returns(2);

        var result = eventEntity.Publish(await talks.CountEventTalksAsync(eventEntity.Id, CancellationToken.None));

        result.IsSuccess.ShouldBeTrue();
        await talks.Received(1).CountEventTalksAsync(eventEntity.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Start_in_published_should_transition_to_InProgress()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published);

        eventEntity.Start().IsSuccess.ShouldBeTrue();

        eventEntity.EventStatus.ShouldBe(EventStatus.InProgress);
        eventEntity.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.InProgress)]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Canceled)]
    public void Start_outside_of_published_should_fail(EventStatus current)
    {
        var eventEntity = EventFactory.WithStatus(current);

        eventEntity.Start().Error.ShouldBe(EventsErrors.InvalidStatusTransition);

        eventEntity.EventStatus.ShouldBe(current);
    }

    [Theory]
    [InlineData(EventStatus.Published)]
    [InlineData(EventStatus.InProgress)]
    public void Close_in_published_or_in_progress_should_close(EventStatus current)
    {
        var eventEntity = EventFactory.WithStatus(current);

        eventEntity.Close().IsSuccess.ShouldBeTrue();

        eventEntity.EventStatus.ShouldBe(EventStatus.Closed);
    }

    [Theory]
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Canceled)]
    public void Close_outside_of_published_or_in_progress_should_fail(EventStatus current)
    {
        var eventEntity = EventFactory.WithStatus(current);

        eventEntity.Close().Error.ShouldBe(EventsErrors.InvalidStatusTransition);

        eventEntity.EventStatus.ShouldBe(current);
    }

    [Theory]
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.Published)]
    [InlineData(EventStatus.InProgress)]
    public void Cancel_with_reason_should_cancel_and_issue_EventCanceled(EventStatus current)
    {
        var eventEntity = EventFactory.WithStatus(current);

        var result = eventEntity.Cancel("  Palestrante indisponível  ");

        result.IsSuccess.ShouldBeTrue();
        eventEntity.EventStatus.ShouldBe(EventStatus.Canceled);
        eventEntity.EventCancellationReason.ShouldBe("Palestrante indisponível");
        var integration = eventEntity.Events.ShouldHaveSingleItem().ShouldBeOfType<EventCanceled>();
        integration.EventId.ShouldBe(eventEntity.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_without_reason_should_fail_with_CancellationReasonRequired(string? reason)
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published);

        var result = eventEntity.Cancel(reason);

        result.Error.ShouldBe(EventsErrors.CancellationReasonRequired);
        eventEntity.EventStatus.ShouldBe(EventStatus.Published);
        eventEntity.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Canceled)]
    public void Cancel_in_closed_or_canceled_should_fail(EventStatus current)
    {
        var eventEntity = EventFactory.WithStatus(current);

        eventEntity.Cancel("reason").Error.ShouldBe(EventsErrors.InvalidStatusTransition);

        eventEntity.EventStatus.ShouldBe(current);
    }

    [Theory]
    [InlineData(EventStatus.Draft, true)]
    [InlineData(EventStatus.Published, true)]
    [InlineData(EventStatus.InProgress, false)]
    [InlineData(EventStatus.Closed, false)]
    [InlineData(EventStatus.Canceled, false)]
    public void Update_only_in_draft_or_published(EventStatus current, bool allowed)
    {
        var eventEntity = EventFactory.WithStatus(current);

        var result = eventEntity.Update("Novo nome", null, EventFactory.Start, EventFactory.End, EventFormat.InPerson, EventFactory.VenueId, null, 10);

        result.IsSuccess.ShouldBe(allowed);
        if (allowed)
        {
            eventEntity.EventName.ShouldBe("Novo nome");
            eventEntity.EventMaximumCapacity.ShouldBe(10);
        }
        else
        {
            result.Error.ShouldBe(EventsErrors.EventCannotBeUpdated);
            eventEntity.EventName.ShouldBe("DevConf");
        }
    }

    [Theory]
    [InlineData(EventStatus.Draft, true)]
    [InlineData(EventStatus.Published, false)]
    [InlineData(EventStatus.InProgress, false)]
    [InlineData(EventStatus.Closed, false)]
    [InlineData(EventStatus.Canceled, true)]
    public void Delete_only_in_draft_or_canceled(EventStatus current, bool allowed)
    {
        var eventEntity = EventFactory.WithStatus(current);

        var result = eventEntity.MarkDeleted();

        result.IsSuccess.ShouldBe(allowed);
        if (!allowed)
        {
            result.Error.ShouldBe(EventsErrors.EventCannotBeDeleted);
        }
    }

    [Theory]
    [InlineData(EventStatus.Draft, false)]
    [InlineData(EventStatus.Published, true)]
    [InlineData(EventStatus.InProgress, true)]
    [InlineData(EventStatus.Closed, false)]
    [InlineData(EventStatus.Canceled, false)]
    public void AcceptsRegistrations_only_in_published_or_in_progress(EventStatus current, bool expected)
    {
        EventFactory.WithStatus(current).AcceptsRegistrations.ShouldBe(expected);
    }
}
