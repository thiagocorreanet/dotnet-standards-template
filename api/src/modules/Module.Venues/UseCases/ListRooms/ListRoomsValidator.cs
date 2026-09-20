using FluentValidation;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.ListRooms;

internal sealed class ListRoomsValidator : AbstractValidator<ListRoomsRequest>
{
    public ListRoomsValidator() => RuleFor(r => r.VenueId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
