using Module.Identity.Domain;
using Shared.Contracts.Identity;
using Shouldly;
namespace Tests.Unit.Identity;
public sealed class UserTests
{
    [Fact]
    public void Subject_opaque_should_be_distinct_of_id_internal()
    {
        var user = User.Create("https://identity.example/realms/app", "federated|opaque/subject", " Maria ", " maria@example.test ");
        user.Id.Version.ShouldBe(7);
        user.Subject.ShouldBe("federated|opaque/subject");
        user.UserName.ShouldBe("Maria");
        user.Events.ShouldHaveSingleItem().ShouldBeOfType<UserRegistered>().UserId.ShouldBe(user.Id);
        typeof(User).GetProperty("PasswordHash").ShouldBeNull();
    }
    [Fact]
    public void Revocation_cannot_move_backwards_in_time()
    {
        var user = User.Create("https://id.example", "subject", "Name", "name@example.test");
        var now = DateTimeOffset.UtcNow;
        user.UpdateAccess(false, now);
        user.UpdateAccess(true, now.AddDays(-1));
        user.TokensValidAfter.ShouldBe(now);
        user.IsActive.ShouldBeTrue();
    }
}
