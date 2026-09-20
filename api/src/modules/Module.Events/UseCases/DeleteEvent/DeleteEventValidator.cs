using FluentValidation;
using Shared.Http.Validation;

namespace Module.Events.UseCases.DeleteEvent;

internal sealed class DeleteEventValidator : AbstractValidator<DeleteEventRequest>
{
    public DeleteEventValidator() => RuleFor(r => r.EventId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
