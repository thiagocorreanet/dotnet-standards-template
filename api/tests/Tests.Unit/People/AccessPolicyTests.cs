using Microsoft.EntityFrameworkCore;
using Module.People.Shared;
using Module.People.UseCases.CreatePerson;
using Module.People.UseCases.DeletePerson;
using Module.People.UseCases.GetPerson;
using Module.People.UseCases.ListPeople;
using Module.People.UseCases.UpdatePerson;
using Shared.Contracts.Common;
using Shouldly;
using Tests.Unit.Support;

namespace Tests.Unit.People;

/// <summary>
/// Matriz das policies de People nos caminhos que decidem sem consultar o banco: o contexto é criado sem conexão.
/// A verificação de titularidade com dados reais está nos testes de integração (IDOR).
/// </summary>
public sealed class AccessPolicyTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly PeopleDbContext _db = new(new DbContextOptionsBuilder<PeopleDbContext>()
        .UseNpgsql("Host=localhost;Database=not_conecta;Username=x;Password=x")
        .Options);

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("unbound", false)]
    [InlineData("participant", false)]
    [InlineData("organizer", false)]
    [InlineData("administrator", true)]
    public async Task Only_administrator_lists_people(string profile, bool allowed) =>
        (await new ListPeopleAccessPolicy(TestUsers.For(profile)).CanExecuteAsync(new ListPeopleRequest(null, null), Ct)).ShouldBe(allowed);

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("unbound", false)]
    [InlineData("administrator", true)]
    public async Task Person_management_requires_local_binding_and_administrator_bypasses_ownership(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);
        var personId = Guid.NewGuid();

        (await new CreatePersonAccessPolicy(_db, user).CanExecuteAsync(Create(Guid.NewGuid()), Ct)).ShouldBe(allowed);
        (await new GetPersonAccessPolicy(Ownership(user), user).CanExecuteAsync(new GetPersonRequest(personId), Ct)).ShouldBe(allowed);
        (await new UpdatePersonAccessPolicy(Ownership(user), user).CanExecuteAsync(Update(personId), Ct)).ShouldBe(allowed);
        (await new DeletePersonAccessPolicy(Ownership(user), user).CanExecuteAsync(new DeletePersonRequest(personId), Ct)).ShouldBe(allowed);
    }

    [Fact]
    public async Task Participant_cannot_create_a_person_bound_to_another_user()
    {
        var user = TestUsers.Participant();

        (await new CreatePersonAccessPolicy(_db, user).CanExecuteAsync(Create(Guid.NewGuid()), Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Participant_without_person_identifier_is_denied()
    {
        var user = TestUsers.Participant();

        (await new GetPersonAccessPolicy(Ownership(user), user).CanExecuteAsync(new GetPersonRequest(Guid.Empty), Ct)).ShouldBeFalse();
        (await new UpdatePersonAccessPolicy(Ownership(user), user).CanExecuteAsync(Update(Guid.Empty), Ct)).ShouldBeFalse();
        (await new DeletePersonAccessPolicy(Ownership(user), user).CanExecuteAsync(new DeletePersonRequest(Guid.Empty), Ct)).ShouldBeFalse();
    }

    private PersonOwnership Ownership(ICurrentUser user) => new(_db, user);

    private static CreatePersonRequest Create(Guid? userId) =>
        new("Pessoa", "pessoa@example.test", null, null, null, null, null, null, userId);

    private static UpdatePersonRequest Update(Guid personId) =>
        new("Pessoa", "pessoa@example.test", null, null, null, null, null, null) { PersonId = personId };
}
