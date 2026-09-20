using FluentValidation.TestHelper;
using Module.Identity.UseCases.RegisterUser;
namespace Tests.Unit.Identity;
public sealed class ValidatorsTests
{
    private readonly RegisterUserValidator validator = new();
    [Fact] public void Subject_opaque_valid() =>
        validator.TestValidate(new RegisterUserRequest("provider|opaque", "Maria", "maria@example.test")).ShouldNotHaveAnyValidationErrors();
    [Theory]
    [InlineData("")]
    [InlineData(" padded ")]
    public void Subject_invalid(string subject) =>
        validator.TestValidate(new RegisterUserRequest(subject, "Maria", "maria@example.test")).ShouldHaveValidationErrorFor(x => x.Subject);
    [Fact] public void Email_invalid() =>
        validator.TestValidate(new RegisterUserRequest("subject", "Maria", "invalid")).ShouldHaveValidationErrorFor(x => x.UserEmail);
}
