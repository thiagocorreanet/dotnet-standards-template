using FluentValidation.TestHelper;
using Module.People.UseCases.ListPeople;

namespace Tests.Unit.People;

public sealed class ListPeopleValidatorTests
{
    private readonly ListPeopleValidator _validator = new();

    [Fact]
    public void Should_accept_default() => _validator.TestValidate(new ListPeopleRequest(null, null)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_require_page_greater_or_equal_the_a(int page) =>
        _validator.TestValidate(new ListPeopleRequest(null, null, page)).ShouldHaveValidationErrorFor(r => r.Page);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Should_limit_size_of_page(int size) =>
        _validator.TestValidate(new ListPeopleRequest(null, null, 1, size)).ShouldHaveValidationErrorFor(r => r.PageSize);

    [Fact]
    public void Should_limit_search_in_100_characters() =>
        _validator.TestValidate(new ListPeopleRequest(new string('x', 101), null)).ShouldHaveValidationErrorFor(r => r.Search);
}
