using FluentValidation;
using Shared.Http.Validation;

namespace Module.People.UseCases.GetPerson;

internal sealed class GetPersonValidator : AbstractValidator<GetPersonRequest>
{
    public GetPersonValidator() => RuleFor(r => r.PersonId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
