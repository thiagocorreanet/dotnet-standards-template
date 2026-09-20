using FluentValidation;
using Shared.Http.Validation;

namespace Module.Events.UseCases.GetEvent;

internal sealed class GetEventValidator : AbstractValidator<GetEventRequest>
{
    public GetEventValidator() => RuleFor(r => r.EventId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
