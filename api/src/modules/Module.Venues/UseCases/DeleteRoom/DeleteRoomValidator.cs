using FluentValidation;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.DeleteRoom;

internal sealed class DeleteRoomValidator : AbstractValidator<DeleteRoomRequest>
{
    public DeleteRoomValidator()
    {
        RuleFor(r => r.VenueId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.RoomId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
    }
}
