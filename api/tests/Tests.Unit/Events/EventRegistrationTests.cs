using Module.Events.Domain;
using NSubstitute;
using Shared.Contracts.Events;
using Shared.Contracts.Venues;
using Shouldly;

namespace Tests.Unit.Events;

public sealed class EventRegistrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_in_published_should_confirm_and_issue_RegistrationCompleted()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published);
        var personId = Guid.NewGuid();

        var result = eventEntity.Register(personId, confirmedRegistrations: 0, venueTotalCapacity: 100, Now);

        result.IsSuccess.ShouldBeTrue();
        var registration = result.Value;
        registration.EventId.ShouldBe(eventEntity.Id);
        registration.PersonId.ShouldBe(personId);
        registration.RegistrationStatus.ShouldBe(RegistrationStatus.Confirmed);
        registration.RegistrationRegisteredAt.ShouldBe(Now);
        registration.RegistrationCanceledAt.ShouldBeNull();
        eventEntity.Registrations.ShouldHaveSingleItem().ShouldBe(registration);
        var integration = eventEntity.Events.ShouldHaveSingleItem().ShouldBeOfType<RegistrationCompleted>();
        integration.RegistrationId.ShouldBe(registration.Id);
        integration.EventId.ShouldBe(eventEntity.Id);
        integration.PersonId.ShouldBe(personId);
    }

    [Fact]
    public void Register_in_progress_should_be_allowed()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.InProgress);

        eventEntity.Register(Guid.NewGuid(), 0, null, Now).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Canceled)]
    public void Register_outside_of_published_or_in_progress_should_fail(EventStatus current)
    {
        var eventEntity = EventFactory.WithStatus(current);

        var result = eventEntity.Register(Guid.NewGuid(), 0, null, Now);

        result.Error.ShouldBe(EventsErrors.EventDoesNotAcceptRegistrations);
        eventEntity.Registrations.ShouldBeEmpty();
    }

    [Fact]
    public void Register_should_respect_capacity_maximum_of_event()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published, capacity: 2);

        eventEntity.Register(Guid.NewGuid(), confirmedRegistrations: 1, venueTotalCapacity: 1000, Now).IsSuccess.ShouldBeTrue();
        var exhausted = eventEntity.Register(Guid.NewGuid(), confirmedRegistrations: 2, venueTotalCapacity: 1000, Now);

        exhausted.Error.ShouldBe(EventsErrors.CapacityExhausted);
    }

    [Fact]
    public async Task Register_without_capacity_maximum_should_use_capacity_of_local()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published, capacity: null);
        var venues = Substitute.For<IVenuesModuleApi>();
        venues.GetVenueSummaryAsync(EventFactory.VenueId, Arg.Any<CancellationToken>())
            .Returns(new VenueSummary(EventFactory.VenueId, "Auditório", VenueTotalCapacity: 3, RoomsCount: 1));
        var venue = await venues.GetVenueSummaryAsync(EventFactory.VenueId, CancellationToken.None);

        eventEntity.EffectiveCapacity(venue!.VenueTotalCapacity).ShouldBe(3);
        eventEntity.Register(Guid.NewGuid(), confirmedRegistrations: 2, venue.VenueTotalCapacity, Now).IsSuccess.ShouldBeTrue();
        eventEntity.Register(Guid.NewGuid(), confirmedRegistrations: 3, venue.VenueTotalCapacity, Now).Error.ShouldBe(EventsErrors.CapacityExhausted);
    }

    [Fact]
    public void Capacity_maximum_of_event_overrides_over_the_of_local()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published, capacity: 5);

        eventEntity.EffectiveCapacity(venueTotalCapacity: 500).ShouldBe(5);
    }

    [Fact]
    public void Event_remote_without_capacity_maximum_not_has_limit()
    {
        var eventEntity = EventFactory.Remote();
        eventEntity.Publish(1);

        eventEntity.EffectiveCapacity(venueTotalCapacity: null).ShouldBeNull();
        eventEntity.Register(Guid.NewGuid(), confirmedRegistrations: 10_000, null, Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Register_person_already_confirmed_in_collection_should_fail_with_PersonAlreadyRegistered()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published);
        var personId = Guid.NewGuid();
        eventEntity.Register(personId, 0, null, Now);

        var result = eventEntity.Register(personId, 1, null, Now);

        result.Error.ShouldBe(EventsErrors.PersonAlreadyRegistered);
        eventEntity.Registrations.Count.ShouldBe(1);
    }

    [Fact]
    public void Register_person_with_registration_canceled_should_allow_new_registration()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published);
        var personId = Guid.NewGuid();
        var first = eventEntity.Register(personId, 0, null, Now).Value;
        eventEntity.CancelRegistration(first.Id, Now.AddMinutes(1));

        var second = eventEntity.Register(personId, 0, null, Now.AddMinutes(2));

        second.IsSuccess.ShouldBeTrue();
        eventEntity.Registrations.Count.ShouldBe(2);
    }

    [Fact]
    public void CancelRegistration_should_change_status_and_issue_RegistrationCanceled()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published);
        var personId = Guid.NewGuid();
        var registration = eventEntity.Register(personId, 0, null, Now).Value;
        eventEntity.ClearEvents();
        var canceledAt = Now.AddHours(1);

        var result = eventEntity.CancelRegistration(registration.Id, canceledAt);

        result.IsSuccess.ShouldBeTrue();
        registration.RegistrationStatus.ShouldBe(RegistrationStatus.Canceled);
        registration.RegistrationCanceledAt.ShouldBe(canceledAt);
        registration.DeletedAt.ShouldBeNull();
        var integration = eventEntity.Events.ShouldHaveSingleItem().ShouldBeOfType<RegistrationCanceled>();
        integration.RegistrationId.ShouldBe(registration.Id);
        integration.PersonId.ShouldBe(personId);
    }

    [Fact]
    public void CancelRegistration_already_canceled_should_fail_with_RegistrationAlreadyCanceled()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published);
        var registration = eventEntity.Register(Guid.NewGuid(), 0, null, Now).Value;
        eventEntity.CancelRegistration(registration.Id, Now);

        var result = eventEntity.CancelRegistration(registration.Id, Now);

        result.Error.ShouldBe(EventsErrors.RegistrationAlreadyCanceled);
    }

    [Fact]
    public void CancelRegistration_missing_should_fail_with_RegistrationNotFound()
    {
        var eventEntity = EventFactory.WithStatus(EventStatus.Published);

        eventEntity.CancelRegistration(Guid.NewGuid(), Now).Error.ShouldBe(EventsErrors.RegistrationNotFound);
    }
}
