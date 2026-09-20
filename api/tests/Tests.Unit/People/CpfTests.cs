using Module.People.Domain;
using Shouldly;

namespace Tests.Unit.People;

public sealed class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData(" 529 982 247 25 ")]
    [InlineData("111.444.777-35")]
    public void IsValid_should_accept_cpf_with_digits_check_correct(string cpf) => Cpf.IsValid(cpf).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("529.982.247-26")]
    [InlineData("111.111.111-11")]
    [InlineData("000.000.000-00")]
    public void IsValid_should_reject_cpf_invalid(string? cpf) => Cpf.IsValid(cpf).ShouldBeFalse();

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData(" 529 982 247 25 ", "52998224725")]
    public void Normalize_should_preserve_only_digits(string input, string expected) => Cpf.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("---")]
    public void Normalize_should_return_null_without_digits(string? input) => Cpf.Normalize(input).ShouldBeNull();
}
