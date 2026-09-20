using FluentValidation.TestHelper;
using Module.People.UseCases.CreatePerson;

namespace Tests.Unit.People;

public sealed class CreatePersonValidatorTests
{
    private readonly CreatePersonValidator _validator = new();

    private static CreatePersonRequest ValidRequest() =>
        new("Maria Silva", "maria@company.com", "(27) 99999-0000", "529.982.247-25", "Globalsys", "Arquiteta", "Bio", "https://photo.local/m.png");

    [Fact]
    public void Should_accept_request_valid() => _validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Should_accept_optional_null() =>
        _validator.TestValidate(new CreatePersonRequest("Maria", "maria@company.com", null, null, null, null, null, null)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_require_name(string name) =>
        _validator.TestValidate(ValidRequest() with { PersonName = name }).ShouldHaveValidationErrorFor(r => r.PersonName);

    [Fact]
    public void Should_limit_size_of_name() =>
        _validator.TestValidate(ValidRequest() with { PersonName = new string('a', 151) }).ShouldHaveValidationErrorFor(r => r.PersonName);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    public void Should_require_email_valid(string email) =>
        _validator.TestValidate(ValidRequest() with { PersonEmail = email }).ShouldHaveValidationErrorFor(r => r.PersonEmail);

    [Theory]
    [InlineData("123")]
    [InlineData("111.111.111-11")]
    [InlineData("529.982.247-26")]
    public void Should_reject_cpf_invalid(string cpf) =>
        _validator.TestValidate(ValidRequest() with { PersonDocument = cpf }).ShouldHaveValidationErrorFor(r => r.PersonDocument);

    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    public void Should_accept_cpf_valid_with_or_without_mask(string cpf) =>
        _validator.TestValidate(ValidRequest() with { PersonDocument = cpf }).ShouldNotHaveValidationErrorFor(r => r.PersonDocument);

    [Fact]
    public void Should_limit_short_bio_in_2000_characters() =>
        _validator.TestValidate(ValidRequest() with { PersonShortBio = new string('b', 2001) }).ShouldHaveValidationErrorFor(r => r.PersonShortBio);

    [Theory]
    [InlineData("photo.png")]
    [InlineData("ftp://servidor/photo.png")]
    [InlineData("javascript:alert(1)")]
    public void Should_reject_url_of_photo_not_http(string url) =>
        _validator.TestValidate(ValidRequest() with { PersonPhotoUrl = url }).ShouldHaveValidationErrorFor(r => r.PersonPhotoUrl);
}
