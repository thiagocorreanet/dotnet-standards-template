using NSubstitute;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;

namespace Tests.Unit.Shared;

/// <summary>Usuários correntes para testar policies sem pipeline HTTP.</summary>
internal static class TestUsers
{
    public static readonly Guid Id = Guid.NewGuid();

    public static ICurrentUser Anonymous() => Create(false, null);

    /// <summary>Token válido, mas sem vínculo local resolvido.</summary>
    public static ICurrentUser WithoutLocalBinding(params string[] roles) => Create(true, null, roles);

    public static ICurrentUser Participant() => Create(true, Id, DefaultRoles.Participant);

    public static ICurrentUser Organizer() => Create(true, Id, DefaultRoles.Organizer);

    public static ICurrentUser Administrator() => Create(true, Id, DefaultRoles.Administrator);

    /// <summary>Perfis usados nas matrizes de teste: anonymous, unbound, participant, organizer, administrator.</summary>
    public static ICurrentUser For(string profile) => profile switch
    {
        "anonymous" => Anonymous(),
        "unbound" => WithoutLocalBinding(DefaultRoles.Administrator),
        "participant" => Participant(),
        "organizer" => Organizer(),
        "administrator" => Administrator(),
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
    };

    private static ICurrentUser Create(bool authenticated, Guid? id, params string[] roles)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(authenticated);
        user.Id.Returns(id);
        user.Roles.Returns(roles);
        user.HasRole(Arg.Any<string>()).Returns(call => roles.Contains(call.Arg<string>()));
        return user;
    }
}
