using FluentValidation;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.UpdateRoom;

internal sealed class UpdateRoomValidator : AbstractValidator<UpdateRoomRequest>
{
    public UpdateRoomValidator()
    {
        RuleFor(r => r.RoomName).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(100).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.RoomCapacity).GreaterThan(0).WithMessage(ValidationMessages.GreaterThanZero);
        RuleFor(r => r.RoomType).IsInEnum();
        RuleFor(r => r.RoomResources).MaximumLength(500).WithMessage(ValidationMessages.MaximumLength);
    }
}
