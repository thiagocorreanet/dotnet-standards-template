using FluentValidation;
using Module.Events.Domain;
using Module.Events.UseCases.CreateEvent;
using Shared.Http.Validation;

namespace Module.Events.UseCases.UpdateEvent;

internal sealed class UpdateEventValidator : AbstractValidator<UpdateEventRequest>
{
    public UpdateEventValidator()
    {
        RuleFor(r => r.EventName).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(200).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.EventDescription).MaximumLength(4000).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.EventStartDate).NotEmpty().WithMessage(ValidationMessages.Required);
        RuleFor(r => r.EventEndDate).NotEmpty().WithMessage(ValidationMessages.Required)
            .GreaterThan(r => r.EventStartDate).WithMessage(EventValidationMessages.EndDateAfterStart);
        RuleFor(r => r.EventFormat).IsInEnum().WithMessage(EventValidationMessages.InvalidFormat);
        RuleFor(r => r.EventRemoteUrl).MaximumLength(500).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.EventMaximumCapacity).GreaterThan(0).When(r => r.EventMaximumCapacity.HasValue).WithMessage(ValidationMessages.GreaterThanZero);
        RuleFor(r => r.VenueId).NotEqual(Guid.Empty).When(r => r.VenueId.HasValue).WithMessage(ValidationMessages.GuidRequired);

        RuleFor(r => r.VenueId).NotNull().WithMessage(EventValidationMessages.VenueRequired)
            .When(r => r.EventFormat is EventFormat.InPerson or EventFormat.Hybrid);
        RuleFor(r => r.VenueId).Null().WithMessage(EventValidationMessages.VenueNotAllowed)
            .When(r => r.EventFormat == EventFormat.Remote);
        RuleFor(r => r.EventRemoteUrl).NotEmpty().WithMessage(EventValidationMessages.LinkRequired)
            .When(r => r.EventFormat is EventFormat.Remote or EventFormat.Hybrid);
    }
}
