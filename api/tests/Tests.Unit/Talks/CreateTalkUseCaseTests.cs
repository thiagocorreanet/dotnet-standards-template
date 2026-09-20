using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Module.Talks.UseCases.CreateTalk;
using NSubstitute;
using Shared.Contracts.Events;
using Shared.Contracts.Venues;
using Shared.Contracts.People;
using Shared.Http.Results;
using Shouldly;

namespace Tests.Unit.Talks;

/// <summary>
/// Regras cruzadas do caso de uso com os contratos dos outros módulos substituídos por NSubstitute.
/// O contexto é instanciado sem conexão: todos os cenários falham antes de qualquer acesso ao banco.
/// </summary>
public sealed class CreateTalkUseCaseTests : IDisposable
{
    private static readonly DateTimeOffset EventStart = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EventEnd = EventStart.AddDays(2);
    private static readonly Guid VenueId = Guid.NewGuid();

    private readonly IEventsModuleApi _events = Substitute.For<IEventsModuleApi>();
    private readonly IVenuesModuleApi _venues = Substitute.For<IVenuesModuleApi>();
    private readonly IPeopleModuleApi _people = Substitute.For<IPeopleModuleApi>();
    private readonly TalksDbContext _db = new(new DbContextOptionsBuilder<TalksDbContext>()
        .UseNpgsql("Host=localhost;Database=not_conecta;Username=x;Password=x")
        .Options);

    public CreateTalkUseCaseTests()
    {
        _events.GetTrackSummaryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => new TrackSummary(call.ArgAt<Guid>(1), call.ArgAt<Guid>(0), "Principal", true));
    }

    private CreateTalkUseCase CreateUseCase() => new(
        _db,
        new TalkScheduleChecker(_events, _venues, NullLogger<TalkScheduleChecker>.Instance),
        _people,
        NullLogger<CreateTalkUseCase>.Instance);

    private static CreateTalkRequest Request(Guid? roomId = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), roomId, "Monolito modular", null, EventStart.AddHours(1), EventStart.AddHours(2),
        [new CreateTalkSpeakerRequest(Guid.NewGuid(), SpeakerRole.Principal)]);

    private static EventSummary Event(Guid id, string status = "Published", Guid? venueId = null) =>
        new(id, "Evento Teste", EventStart, EventEnd, "InPerson", status, venueId ?? VenueId);


    [Fact]
    public async Task Event_missing_should_return_422_EventNotFound()
    {
        _events.GetEventSummaryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((EventSummary?)null);

        var result = await CreateUseCase().HandleAsync(Request(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TalksErrors.EventNotFound);
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Canceled")]
    public async Task Event_closed_or_canceled_should_return_422_EventDoesNotAcceptTalks(string status)
    {
        var request = Request();
        _events.GetEventSummaryAsync(request.EventId, Arg.Any<CancellationToken>()).Returns(Event(request.EventId, status));

        var result = await CreateUseCase().HandleAsync(request, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TalksErrors.EventDoesNotAcceptTalks);
        result.Error.Code.ShouldBe("Talks.EventDoesNotAcceptTalks");
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        await _people.DidNotReceiveWithAnyArgs().GetPeopleSummaryAsync(default!, default);
    }

    [Fact]
    public async Task Period_outside_of_event_should_return_422_PeriodOutsideEvent()
    {
        var request = Request() with { TalkStart = EventEnd.AddMinutes(-30), TalkEnd = EventEnd.AddMinutes(30) };
        _events.GetEventSummaryAsync(request.EventId, Arg.Any<CancellationToken>()).Returns(Event(request.EventId));

        var result = await CreateUseCase().HandleAsync(request, CancellationToken.None);

        result.Error.ShouldBe(TalksErrors.PeriodOutsideEvent);
    }

    [Fact]
    public async Task Room_missing_should_return_422_RoomNotFound()
    {
        var request = Request(roomId: Guid.NewGuid());
        _events.GetEventSummaryAsync(request.EventId, Arg.Any<CancellationToken>()).Returns(Event(request.EventId));
        _venues.GetRoomSummaryAsync(request.RoomId!.Value, Arg.Any<CancellationToken>()).Returns((RoomSummary?)null);

        var result = await CreateUseCase().HandleAsync(request, CancellationToken.None);

        result.Error.ShouldBe(TalksErrors.RoomNotFound);
    }

    [Fact]
    public async Task Room_of_another_local_should_return_422_RoomDoesNotBelongToVenue()
    {
        var request = Request(roomId: Guid.NewGuid());
        _events.GetEventSummaryAsync(request.EventId, Arg.Any<CancellationToken>()).Returns(Event(request.EventId));
        _venues.GetRoomSummaryAsync(request.RoomId!.Value, Arg.Any<CancellationToken>())
            .Returns(new RoomSummary(request.RoomId.Value, Guid.NewGuid(), "Auditório", 100, "Auditorium"));

        var result = await CreateUseCase().HandleAsync(request, CancellationToken.None);

        result.Error.ShouldBe(TalksErrors.RoomDoesNotBelongToVenue);
    }

    [Fact]
    public async Task Event_online_without_local_with_room_provided_should_return_RoomDoesNotBelongToVenue()
    {
        var request = Request(roomId: Guid.NewGuid());
        _events.GetEventSummaryAsync(request.EventId, Arg.Any<CancellationToken>())
            .Returns(new EventSummary(request.EventId, "Online", EventStart, EventEnd, "Online", "Published", null));
        _venues.GetRoomSummaryAsync(request.RoomId!.Value, Arg.Any<CancellationToken>())
            .Returns(new RoomSummary(request.RoomId.Value, VenueId, "Auditório", 100, "Auditorium"));

        var result = await CreateUseCase().HandleAsync(request, CancellationToken.None);

        result.Error.ShouldBe(TalksErrors.RoomDoesNotBelongToVenue);
    }

    [Fact]
    public async Task Person_missing_should_return_422_PersonNotFound()
    {
        var request = Request();
        _events.GetEventSummaryAsync(request.EventId, Arg.Any<CancellationToken>()).Returns(Event(request.EventId));
        _people.GetPeopleSummaryAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateUseCase().HandleAsync(request, CancellationToken.None);

        result.Error.ShouldBe(TalksErrors.PersonNotFound);
        await _people.Received(1).GetPeopleSummaryAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _db.Dispose();
}
