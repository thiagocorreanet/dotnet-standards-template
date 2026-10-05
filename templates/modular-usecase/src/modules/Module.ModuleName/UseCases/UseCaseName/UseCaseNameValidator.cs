using FluentValidation;
using Shared.Http.Validation;

namespace Module.ModuleName.UseCases.UseCaseName;

/// <summary>Formato, tamanho e obrigatoriedade. Regra de negócio fica no Domain.</summary>
internal sealed class UseCaseNameValidator : AbstractValidator<UseCaseNameRequest>
{
    public UseCaseNameValidator()
    {
#if (command)
        RuleFor(r => r.Description)
            .NotEmpty().WithMessage(ValidationMessages.Required)
            .MaximumLength(200).WithMessage(ValidationMessages.MaximumLength);
#else
        RuleFor(r => r.Id).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
#endif
    }
}
