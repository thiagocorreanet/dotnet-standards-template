using FluentValidation;
using Shared.Http.Validation;

namespace Module.People.UseCases.DeletePerson;

internal sealed class DeletePersonValidator : AbstractValidator<DeletePersonRequest>
{
    public DeletePersonValidator() => RuleFor(r => r.PersonId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
