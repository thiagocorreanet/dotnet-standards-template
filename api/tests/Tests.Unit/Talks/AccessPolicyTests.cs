using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Module.Talks.UseCases.AddContent;
using Module.Talks.UseCases.AddSpeaker;
using Module.Talks.UseCases.CreateTalk;
using Module.Talks.UseCases.DeleteTalk;
using Module.Talks.UseCases.GetTalk;
using Module.Talks.UseCases.IssueCertificate;
using Module.Talks.UseCases.ListAttendances;
using Module.Talks.UseCases.ListTalks;
using Module.Talks.UseCases.RecordAttendance;
using Module.Talks.UseCases.RemoveContent;
using Module.Talks.UseCases.RemoveSpeaker;
using Module.Talks.UseCases.UpdateTalk;
using Module.Talks.UseCases.ValidateCertificate;
using NSubstitute;
using Shared.Contracts.Common;
using Shared.Contracts.Events;
using Shared.Contracts.People;
using Shouldly;
using Tests.Unit.Support;

namespace Tests.Unit.Talks;

/// <summary>
/// Matriz das policies de Talks nos caminhos que decidem sem consultar o banco: o contexto é criado sem conexão e o
/// contrato de Events é substituído. A busca do evento da palestra com dados reais está nos testes de integração.
/// </summary>
public sealed class AccessPolicyTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly IEventsModuleApi _events = Substitute.For<IEventsModuleApi>();
    private readonly IPeopleModuleApi _people = Substitute.For<IPeopleModuleApi>();
    private readonly TalksDbContext _db = new(new DbContextOptionsBuilder<TalksDbContext>()
        .UseNpgsql("Host=localhost;Database=not_conecta;Username=x;Password=x")
        .Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Certificate_validation_is_public() =>
        (await new ValidateCertificateAccessPolicy().CanExecuteAsync(new ValidateCertificateRequest("CODE"), Ct)).ShouldBeTrue();

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("unbound", false)]
    [InlineData("participant", true)]
    public async Task Reads_require_authentication_with_local_binding(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);

        (await new ListTalksAccessPolicy(user).CanExecuteAsync(new ListTalksRequest(null, null), Ct)).ShouldBe(allowed);
        (await new GetTalkAccessPolicy(user).CanExecuteAsync(new GetTalkRequest(Guid.NewGuid()), Ct)).ShouldBe(allowed);
    }

    [Theory]
    [InlineData("anonymous", false, false)]
    [InlineData("participant", true, false)]
    [InlineData("organizer", false, false)]
    [InlineData("organizer", true, true)]
    [InlineData("administrator", false, true)]
    public async Task Talk_creation_is_for_administrator_or_owner_organizer(string profile, bool ownsEvent, bool allowed)
    {
        var user = TestUsers.For(profile);
        var eventId = Guid.NewGuid();
        _events.BelongsToOrganizerAsync(eventId, TestUsers.Id, Arg.Any<CancellationToken>()).Returns(ownsEvent);

        var decision = await new CreateTalkAccessPolicy(Organizer(user), user).CanExecuteAsync(
            new CreateTalkRequest(eventId, Guid.NewGuid(), null, "Palestra", null, default, default, []), Ct);

        decision.ShouldBe(allowed);
    }

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("unbound", false)]
    [InlineData("administrator", true)]
    public async Task Talk_management_follows_authentication_and_administrator_rules(string profile, bool allowed)
    {
        foreach (var (name, decision) in TalkManagement(TestUsers.For(profile), Guid.NewGuid()))
            (await decision).ShouldBe(allowed, name);
    }

    [Theory]
    [InlineData("participant")]
    [InlineData("organizer")]
    public async Task Talk_management_without_talk_identifier_is_denied(string profile)
    {
        foreach (var (name, decision) in TalkManagement(TestUsers.For(profile), Guid.Empty))
            (await decision).ShouldBeFalse(name);
        await _people.DidNotReceiveWithAnyArgs().BelongsToUserAsync(default, default, default);
    }

    private TalkOrganizerAccess Organizer(ICurrentUser user) => new(_db, user, _events);

    /// <summary>Policies que exigem administrador ou organizador dono do evento da palestra.</summary>
    private IEnumerable<(string Name, Task<bool> Decision)> TalkManagement(ICurrentUser user, Guid talkId)
    {
        var organizer = Organizer(user);
        yield return (nameof(UpdateTalkAccessPolicy), new UpdateTalkAccessPolicy(organizer, user).CanExecuteAsync(
            new UpdateTalkRequest(Guid.NewGuid(), null, "Palestra", null, default, default) { TalkId = talkId }, Ct));
        yield return (nameof(DeleteTalkAccessPolicy), new DeleteTalkAccessPolicy(organizer, user).CanExecuteAsync(new DeleteTalkRequest(talkId), Ct));
        yield return (nameof(AddContentAccessPolicy), new AddContentAccessPolicy(organizer, user).CanExecuteAsync(
            new AddContentRequest("Slides", ContentType.Slides, "https://example.test/slides", null) { TalkId = talkId }, Ct));
        yield return (nameof(RemoveContentAccessPolicy), new RemoveContentAccessPolicy(organizer, user).CanExecuteAsync(
            new RemoveContentRequest(talkId, Guid.NewGuid()), Ct));
        yield return (nameof(AddSpeakerAccessPolicy), new AddSpeakerAccessPolicy(organizer, user).CanExecuteAsync(
            new AddSpeakerRequest(Guid.NewGuid(), SpeakerRole.Principal) { TalkId = talkId }, Ct));
        yield return (nameof(RemoveSpeakerAccessPolicy), new RemoveSpeakerAccessPolicy(organizer, user).CanExecuteAsync(
            new RemoveSpeakerRequest(talkId, Guid.NewGuid()), Ct));
        yield return (nameof(RecordAttendanceAccessPolicy), new RecordAttendanceAccessPolicy(organizer, user).CanExecuteAsync(
            new RecordAttendanceRequest(Guid.NewGuid()) { TalkId = talkId }, Ct));
        yield return (nameof(ListAttendancesAccessPolicy), new ListAttendancesAccessPolicy(organizer, user).CanExecuteAsync(
            new ListAttendancesRequest(talkId), Ct));
        yield return (nameof(IssueCertificateAccessPolicy), new IssueCertificateAccessPolicy(organizer, _people, user).CanExecuteAsync(
            new IssueCertificateRequest(Guid.NewGuid()) { TalkId = talkId }, Ct));
    }
}
