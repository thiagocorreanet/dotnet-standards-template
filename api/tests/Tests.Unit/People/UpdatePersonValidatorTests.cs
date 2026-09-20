using FluentValidation.TestHelper;
using Module.People.UseCases.UpdatePerson;

namespace Tests.Unit.People;

public sealed class UpdatePersonValidatorTests
{
    private readonly UpdatePersonValidator _validator = new();

    private static UpdatePersonRequest ValidRequest() =>
        new("Maria Silva", "maria@company.com", null, "529.982.247-25", "Globalsys", null, null, null, IsActive: false) { PersonId = Guid.NewGuid() };

    [Fact]
    public void Should_accept_request_valid() => _validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Should_require_name() =>
        _validator.TestValidate(ValidRequest() with { PersonName = "" }).ShouldHaveValidationErrorFor(r => r.PersonName);

    [Fact]
    public void Should_require_email_valid() =>
        _validator.TestValidate(ValidRequest() with { PersonEmail = "invalid" }).ShouldHaveValidationErrorFor(r => r.PersonEmail);

    [Fact]
    public void Should_reject_cpf_invalid() =>
        _validator.TestValidate(ValidRequest() with { PersonDocument = "123.456.789-00" }).ShouldHaveValidationErrorFor(r => r.PersonDocument);

    [Fact]
    public void Should_limit_size_of_jobTitle() =>
        _validator.TestValidate(ValidRequest() with { PersonJobTitle = new string('c', 101) }).ShouldHaveValidationErrorFor(r => r.PersonJobTitle);
}
