using FluentValidation;
using Module.Events.UseCases.CreateEvent;
using Shared.Http.Validation;

namespace Module.Events.UseCases.ChangeEventStatus;

internal sealed class ChangeEventStatusValidator : AbstractValidator<ChangeEventStatusRequest>
{
    public ChangeEventStatusValidator()
    {
        RuleFor(r => r.EventStatus).IsInEnum().WithMessage(EventValidationMessages.InvalidStatus);
        RuleFor(r => r.Reason).MaximumLength(1000).WithMessage(ValidationMessages.MaximumLength);
    }
}
