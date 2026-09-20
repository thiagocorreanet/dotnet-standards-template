using FluentValidation;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.UpdateVenue;

internal sealed class UpdateVenueValidator : AbstractValidator<UpdateVenueRequest>
{
    public UpdateVenueValidator()
    {
        RuleFor(r => r.VenueName).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(150).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.VenueDescription).MaximumLength(1000).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.AddressCity).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(100).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.AddressState).NotEmpty().WithMessage(ValidationMessages.Required).Length(2).WithMessage("O campo {PropertyName} deve ter 2 letras (UF).");
    }
}
