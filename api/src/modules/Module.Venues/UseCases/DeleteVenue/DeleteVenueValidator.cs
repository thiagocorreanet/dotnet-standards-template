using FluentValidation;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.DeleteVenue;

internal sealed class DeleteVenueValidator : AbstractValidator<DeleteVenueRequest>
{
    public DeleteVenueValidator() => RuleFor(r => r.VenueId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
