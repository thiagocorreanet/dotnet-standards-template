using FluentValidation;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.GetVenue;

internal sealed class GetVenueValidator : AbstractValidator<GetVenueRequest>
{
    public GetVenueValidator() => RuleFor(r => r.VenueId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
