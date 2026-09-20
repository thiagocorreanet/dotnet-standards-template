using FluentValidation;
using Shared.Http.Validation;

namespace Module.Events.UseCases.RegisterParticipant;

internal sealed class RegisterParticipantValidator : AbstractValidator<RegisterParticipantRequest>
{
    public RegisterParticipantValidator() => RuleFor(r => r.PersonId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
