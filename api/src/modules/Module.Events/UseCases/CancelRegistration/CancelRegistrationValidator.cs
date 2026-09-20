using FluentValidation;
using Shared.Http.Validation;

namespace Module.Events.UseCases.CancelRegistration;

internal sealed class CancelRegistrationValidator : AbstractValidator<CancelRegistrationRequest>
{
    public CancelRegistrationValidator()
    {
        RuleFor(r => r.EventId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.RegistrationId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
    }
}
