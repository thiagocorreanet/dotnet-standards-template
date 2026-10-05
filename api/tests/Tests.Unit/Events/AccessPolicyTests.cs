using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Module.Events.UseCases.AddTrack;
using Module.Events.UseCases.CancelRegistration;
using Module.Events.UseCases.ChangeEventStatus;
using Module.Events.UseCases.CreateEvent;
using Module.Events.UseCases.DeleteEvent;
using Module.Events.UseCases.DeleteTrack;
using Module.Events.UseCases.GetEvent;
using Module.Events.UseCases.ListEvents;
using Module.Events.UseCases.ListRegistrations;
using Module.Events.UseCases.ListTracks;
using Module.Events.UseCases.RegisterParticipant;
using Module.Events.UseCases.UpdateEvent;
using Module.Events.UseCases.UpdateTrack;
using NSubstitute;
using Shared.Contracts.Common;
using Shared.Contracts.People;
using Shouldly;
using Tests.Unit.Shared;

namespace Tests.Unit.Events;

/// <summary>
/// Matriz das policies de Events nos caminhos que decidem sem consultar o banco: o contexto é criado sem conexão.
/// A propriedade do evento com dados reais está nos testes de integração (IDOR e concorrência).
/// </summary>
public sealed class AccessPolicyTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly IPeopleModuleApi _people = Substitute.For<IPeopleModuleApi>();
    private readonly EventsDbContext _db = new(new DbContextOptionsBuilder<EventsDbContext>()
        .UseNpgsql("Host=localhost;Database=not_conecta;Username=x;Password=x")
        .Options);

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("unbound", false)]
    [InlineData("participant", true)]
    public async Task Reads_require_authentication_with_local_binding(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);

        (await new ListEventsAccessPolicy(user).CanExecuteAsync(new ListEventsRequest(null, null, null, null, null), Ct)).ShouldBe(allowed);
        (await new GetEventAccessPolicy(user).CanExecuteAsync(new GetEventRequest(Guid.NewGuid()), Ct)).ShouldBe(allowed);
        (await new ListTracksAccessPolicy(user).CanExecuteAsync(new ListTracksRequest(Guid.NewGuid(), null), Ct)).ShouldBe(allowed);
    }

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("unbound", false)]
    [InlineData("participant", false)]
    [InlineData("organizer", true)]
    [InlineData("administrator", true)]
    public async Task Organizer_or_administrator_creates_events(string profile, bool allowed) =>
        (await new CreateEventAccessPolicy(TestUsers.For(profile)).CanExecuteAsync(
            new CreateEventRequest("Evento", null, default, default, EventFormat.Remote, null, null, null), Ct)).ShouldBe(allowed);

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("unbound", false)]
    [InlineData("participant", false)]
    [InlineData("administrator", true)]
    public async Task Event_management_is_for_administrator_or_owner_organizer(string profile, bool allowed)
    {
        foreach (var (name, decision) in EventManagement(TestUsers.For(profile), Guid.NewGuid()))
            (await decision).ShouldBe(allowed, name);
    }

    [Theory]
    [InlineData("participant")]
    [InlineData("organizer")]
    public async Task Event_management_without_event_identifier_is_denied(string profile)
    {
        foreach (var (name, decision) in EventManagement(TestUsers.For(profile), Guid.Empty))
            (await decision).ShouldBeFalse(name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Participant_registers_only_a_person_bound_to_the_own_account(bool ownsPerson)
    {
        var user = TestUsers.Participant();
        var personId = Guid.NewGuid();
        _people.BelongsToUserAsync(personId, TestUsers.Id, Arg.Any<CancellationToken>()).Returns(ownsPerson);

        var allowed = await new RegisterParticipantAccessPolicy(new EventOrganizerAccess(_db, user), _people, user)
            .CanExecuteAsync(new RegisterParticipantRequest(personId) { EventId = Guid.NewGuid() }, Ct);

        allowed.ShouldBe(ownsPerson);
    }

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("administrator", true)]
    public async Task Registration_changes_follow_authentication_and_administrator_rules(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);
        var eventId = Guid.NewGuid();

        (await new RegisterParticipantAccessPolicy(new EventOrganizerAccess(_db, user), _people, user)
            .CanExecuteAsync(new RegisterParticipantRequest(Guid.NewGuid()) { EventId = eventId }, Ct)).ShouldBe(allowed);
        (await new CancelRegistrationAccessPolicy(_db, new EventOrganizerAccess(_db, user), _people, user)
            .CanExecuteAsync(new CancelRegistrationRequest(eventId, Guid.NewGuid()), Ct)).ShouldBe(allowed);
    }

    [Fact]
    public async Task Registration_changes_without_event_identifier_are_denied_before_checking_the_person()
    {
        var user = TestUsers.Participant();

        (await new RegisterParticipantAccessPolicy(new EventOrganizerAccess(_db, user), _people, user)
            .CanExecuteAsync(new RegisterParticipantRequest(Guid.NewGuid()), Ct)).ShouldBeFalse();
        (await new CancelRegistrationAccessPolicy(_db, new EventOrganizerAccess(_db, user), _people, user)
            .CanExecuteAsync(new CancelRegistrationRequest(Guid.Empty, Guid.NewGuid()), Ct)).ShouldBeFalse();
        await _people.DidNotReceiveWithAnyArgs().BelongsToUserAsync(default, default, default);
    }

    /// <summary>
    /// Policies que exigem administrador ou organizador dono. Sem o perfil Organizer, a regra de dono não consulta o banco.
    /// </summary>
    private IEnumerable<(string Name, Task<bool> Decision)> EventManagement(ICurrentUser user, Guid eventId)
    {
        var organizer = new EventOrganizerAccess(_db, user);
        yield return (nameof(UpdateEventAccessPolicy), new UpdateEventAccessPolicy(organizer, user).CanExecuteAsync(
            new UpdateEventRequest("Evento", null, default, default, EventFormat.Remote, null, null, null) { EventId = eventId }, Ct));
        yield return (nameof(DeleteEventAccessPolicy), new DeleteEventAccessPolicy(organizer, user).CanExecuteAsync(new DeleteEventRequest(eventId), Ct));
        yield return (nameof(ChangeEventStatusAccessPolicy), new ChangeEventStatusAccessPolicy(organizer, user).CanExecuteAsync(
            new ChangeEventStatusRequest(EventStatus.Published, null) { EventId = eventId }, Ct));
        yield return (nameof(AddTrackAccessPolicy), new AddTrackAccessPolicy(organizer, user).CanExecuteAsync(
            new AddTrackRequest("Trilha", null, null) { EventId = eventId }, Ct));
        yield return (nameof(UpdateTrackAccessPolicy), new UpdateTrackAccessPolicy(organizer, user).CanExecuteAsync(
            new UpdateTrackRequest("Trilha", null, null, true) { EventId = eventId }, Ct));
        yield return (nameof(DeleteTrackAccessPolicy), new DeleteTrackAccessPolicy(organizer, user).CanExecuteAsync(
            new DeleteTrackRequest(eventId, Guid.NewGuid()), Ct));
        yield return (nameof(ListRegistrationsAccessPolicy), new ListRegistrationsAccessPolicy(organizer, user).CanExecuteAsync(
            new ListRegistrationsRequest(null) { EventId = eventId }, Ct));
    }
}
