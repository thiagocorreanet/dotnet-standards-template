using Module.Events.Domain;
using Module.Events.UseCases.ChangeEventStatus;
using Module.Events.UseCases.UpdateEvent;
using Module.Events.UseCases.CreateEvent;
using Module.Events.UseCases.RegisterParticipant;
using Module.Events.UseCases.ListEvents;
using Module.Events.UseCases.ListRegistrations;
using Shouldly;

namespace Tests.Unit.Events;

public sealed class CreateEventValidatorTests
{
    private readonly CreateEventValidator _validator = new();
    private static readonly Guid Venue = Guid.NewGuid();
    private const string Link = "https://meet.exemplo.com/room";

    private static CreateEventRequest Valid(EventFormat format = EventFormat.InPerson, Guid? venueId = null, string? link = null) =>
        new("DevConf", "Descrição", EventFactory.Start, EventFactory.End, format,
            venueId ?? (format == EventFormat.Remote ? null : Venue),
            link ?? (format == EventFormat.InPerson ? null : Link), 100);

    [Theory]
    [InlineData(EventFormat.InPerson)]
    [InlineData(EventFormat.Remote)]
    [InlineData(EventFormat.Hybrid)]
    public void Request_consistent_should_be_valid(EventFormat format)
    {
        _validator.Validate(Valid(format)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_required(string name)
    {
        var result = _validator.Validate(Valid() with { EventName = name });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateEventRequest.EventName));
    }

    [Fact]
    public void Name_with_more_of_200_characters_invalid()
    {
        _validator.Validate(Valid() with { EventName = new string('a', 201) }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Description_with_more_of_4000_characters_invalid()
    {
        _validator.Validate(Valid() with { EventDescription = new string('a', 4001) }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void EndDate_should_be_later_the_StartDate()
    {
        var equal = _validator.Validate(Valid() with { EventEndDate = EventFactory.Start });
        var previous = _validator.Validate(Valid() with { EventEndDate = EventFactory.Start.AddHours(-1) });

        equal.Errors.ShouldContain(e => e.PropertyName == nameof(CreateEventRequest.EventEndDate));
        previous.Errors.ShouldContain(e => e.PropertyName == nameof(CreateEventRequest.EventEndDate));
    }

    [Fact]
    public void Format_outside_of_enum_invalid()
    {
        _validator.Validate(Valid() with { EventFormat = (EventFormat)99 }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void InPerson_without_local_invalid()
    {
        var result = _validator.Validate(Valid(EventFormat.InPerson) with { VenueId = null });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateEventRequest.VenueId));
    }

    [Fact]
    public void Remote_without_link_invalid()
    {
        var result = _validator.Validate(Valid(EventFormat.Remote) with { EventRemoteUrl = null });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateEventRequest.EventRemoteUrl));
    }

    [Fact]
    public void Remote_with_local_invalid()
    {
        var result = _validator.Validate(Valid(EventFormat.Remote) with { VenueId = Venue });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateEventRequest.VenueId));
    }

    [Fact]
    public void Hybrid_without_local_or_without_link_invalid()
    {
        _validator.Validate(Valid(EventFormat.Hybrid) with { VenueId = null }).IsValid.ShouldBeFalse();
        _validator.Validate(Valid(EventFormat.Hybrid) with { EventRemoteUrl = "" }).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Capacity_should_be_greater_than_zero_when_provided(int capacity)
    {
        _validator.Validate(Valid() with { EventMaximumCapacity = capacity }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Capacity_null_and_valid()
    {
        _validator.Validate(Valid() with { EventMaximumCapacity = null }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void VenueId_empty_invalid()
    {
        _validator.Validate(Valid() with { VenueId = Guid.Empty }).IsValid.ShouldBeFalse();
    }
}

public sealed class UpdateEventValidatorTests
{
    private readonly UpdateEventValidator _validator = new();
    private static readonly Guid Venue = Guid.NewGuid();

    private static UpdateEventRequest Valid() =>
        new("DevConf", null, EventFactory.Start, EventFactory.End, EventFormat.InPerson, Venue, null, null) { EventId = Guid.NewGuid() };

    [Fact]
    public void Request_consistent_should_be_valid() => _validator.Validate(Valid()).IsValid.ShouldBeTrue();

    [Fact]
    public void Name_required() => _validator.Validate(Valid() with { EventName = "" }).IsValid.ShouldBeFalse();

    [Fact]
    public void EndDate_earlier_invalid() =>
        _validator.Validate(Valid() with { EventEndDate = EventFactory.Start.AddDays(-1) }).IsValid.ShouldBeFalse();

    [Fact]
    public void Remote_with_local_invalid() =>
        _validator.Validate(Valid() with { EventFormat = EventFormat.Remote, EventRemoteUrl = "https://x" }).IsValid.ShouldBeFalse();

    [Fact]
    public void Remote_without_local_with_link_valid() =>
        _validator.Validate(Valid() with { EventFormat = EventFormat.Remote, VenueId = null, EventRemoteUrl = "https://x" }).IsValid.ShouldBeTrue();

    [Fact]
    public void Hybrid_without_link_invalid() =>
        _validator.Validate(Valid() with { EventFormat = EventFormat.Hybrid }).IsValid.ShouldBeFalse();
}

public sealed class ChangeEventStatusValidatorTests
{
    private readonly ChangeEventStatusValidator _validator = new();

    [Theory]
    [InlineData(EventStatus.Published)]
    [InlineData(EventStatus.InProgress)]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Canceled)]
    [InlineData(EventStatus.Draft)]
    public void Statuses_of_enum_are_valid_in_validator(EventStatus status)
    {
        // A validade da transição (inclusive voltar para Rascunho) é regra de domínio (422), não de validação (400).
        _validator.Validate(new ChangeEventStatusRequest(status, null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Status_outside_of_enum_invalid() =>
        _validator.Validate(new ChangeEventStatusRequest((EventStatus)42, null)).IsValid.ShouldBeFalse();

    [Fact]
    public void Cancel_without_reason_passes_in_validator_because_the_domain_responds_422()
    {
        _validator.Validate(new ChangeEventStatusRequest(EventStatus.Canceled, null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Reason_with_more_of_1000_characters_invalid() =>
        _validator.Validate(new ChangeEventStatusRequest(EventStatus.Canceled, new string('m', 1001))).IsValid.ShouldBeFalse();
}

public sealed class RegisterParticipantValidatorTests
{
    private readonly RegisterParticipantValidator _validator = new();

    [Fact]
    public void PersonId_required() => _validator.Validate(new RegisterParticipantRequest(Guid.Empty)).IsValid.ShouldBeFalse();

    [Fact]
    public void PersonId_valid() => _validator.Validate(new RegisterParticipantRequest(Guid.NewGuid())).IsValid.ShouldBeTrue();
}

public sealed class ListEventsValidatorTests
{
    private readonly ListEventsValidator _validator = new();

    [Fact]
    public void Default_valid() => _validator.Validate(new ListEventsRequest(null, null, null, null, null)).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Paging_outside_of_bounds_invalid(int page, int size) =>
        _validator.Validate(new ListEventsRequest(null, null, null, null, null, page, size)).IsValid.ShouldBeFalse();

    [Fact]
    public void Search_with_more_of_100_characters_invalid() =>
        _validator.Validate(new ListEventsRequest(new string('b', 101), null, null, null, null)).IsValid.ShouldBeFalse();

    [Fact]
    public void Interval_of_dates_reversed_invalid() =>
        _validator.Validate(new ListEventsRequest(null, null, null, EventFactory.End, EventFactory.Start)).IsValid.ShouldBeFalse();

    [Fact]
    public void Filters_by_enum_valid() =>
        _validator.Validate(new ListEventsRequest("dev", EventStatus.Published, EventFormat.Hybrid, EventFactory.Start, EventFactory.End)).IsValid.ShouldBeTrue();
}

public sealed class ListRegistrationsValidatorTests
{
    private readonly ListRegistrationsValidator _validator = new();

    [Fact]
    public void Default_valid() => _validator.Validate(new ListRegistrationsRequest(null)).IsValid.ShouldBeTrue();

    [Fact]
    public void Filter_by_status_valid() => _validator.Validate(new ListRegistrationsRequest(RegistrationStatus.Canceled)).IsValid.ShouldBeTrue();

    [Fact]
    public void PageSize_above_of_100_invalid() => _validator.Validate(new ListRegistrationsRequest(null, 1, 101)).IsValid.ShouldBeFalse();
}
