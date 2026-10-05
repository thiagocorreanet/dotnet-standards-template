using Module.Identity.UseCases.GetCurrentUser;
using Module.Identity.UseCases.ListUsers;
using Module.Identity.UseCases.RegisterUser;
using Module.Identity.UseCases.UpdateUserAccess;
using Shouldly;
using Tests.Unit.Support;

namespace Tests.Unit.Identity;

public sealed class AccessPolicyTests
{
    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("participant", true)]
    [InlineData("administrator", true)]
    public async Task Current_user_reads_only_the_own_profile(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);
        var policy = new GetCurrentUserAccessPolicy(user);

        (await policy.CanExecuteAsync(new GetCurrentUserRequest(TestUsers.Id), CancellationToken.None)).ShouldBe(allowed);
        (await policy.CanExecuteAsync(new GetCurrentUserRequest(Guid.NewGuid()), CancellationToken.None)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("participant", false)]
    [InlineData("organizer", false)]
    [InlineData("administrator", true)]
    public async Task Only_administrator_manages_identity_bindings(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);
        var ct = CancellationToken.None;

        (await new ListUsersAccessPolicy(user).CanExecuteAsync(new ListUsersRequest(), ct)).ShouldBe(allowed);
        (await new RegisterUserAccessPolicy(user).CanExecuteAsync(new RegisterUserRequest("subject", "name", "user@example.test"), ct)).ShouldBe(allowed);
        (await new UpdateUserAccessAccessPolicy(user).CanExecuteAsync(new UpdateUserAccessRequest(Guid.NewGuid(), false, true), ct)).ShouldBe(allowed);
    }
}
