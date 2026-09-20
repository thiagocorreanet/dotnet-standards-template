using Module.Talks.Domain;
using Shouldly;

namespace Tests.Unit.Talks;

public class CertificateCodeGeneratorTests
{
    [Fact]
    public void Generate_should_generate_12_characters_of_alphabet_without_ambiguous()
    {
        for (var i = 0; i < 500; i++)
        {
            var code = CertificateCodeGenerator.Generate();

            code.Length.ShouldBe(12);
            code.ShouldAllBe(c => CertificateCodeGenerator.Alphabet.Contains(c));
            code.ShouldNotContain('0');
            code.ShouldNotContain('O');
            code.ShouldNotContain('1');
            code.ShouldNotContain('I');
            code.ShouldNotContain('L');
        }
    }

    [Fact]
    public void Generate_should_generate_codes_distinct()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => CertificateCodeGenerator.Generate()).ToHashSet(StringComparer.Ordinal);

        codes.Count.ShouldBe(1000);
    }

    [Fact]
    public void Alphabet_should_contain_only_the_31_symbols_not_ambiguous()
    {
        CertificateCodeGenerator.Alphabet.Length.ShouldBe(31);
        CertificateCodeGenerator.Alphabet.Distinct().Count().ShouldBe(31);
    }
}
