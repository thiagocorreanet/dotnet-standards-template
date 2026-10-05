using Module.ModuleName.UseCases.UseCaseName;
using Shouldly;

namespace Tests.Unit.ModuleName;

public sealed class UseCaseNameTests
{
#if (command)
    private static readonly UseCaseNameRequest Valid = new("Descrição");
    private static readonly UseCaseNameRequest Invalid = new("");
#else
    private static readonly UseCaseNameRequest Valid = new(Guid.NewGuid());
    private static readonly UseCaseNameRequest Invalid = new(Guid.Empty);
#endif

    // Ao escrever a regra da policy, troque este teste pela matriz de acesso (permitido e negado).
    [Fact]
    public async Task Access_is_denied_until_the_policy_is_written() =>
        (await new UseCaseNameAccessPolicy().CanExecuteAsync(Valid, CancellationToken.None)).ShouldBeFalse();

    [Fact]
    public void Validator_accepts_valid_input_and_rejects_empty_input()
    {
        var validator = new UseCaseNameValidator();

        validator.Validate(Valid).IsValid.ShouldBeTrue();
        validator.Validate(Invalid).IsValid.ShouldBeFalse();
    }
}
