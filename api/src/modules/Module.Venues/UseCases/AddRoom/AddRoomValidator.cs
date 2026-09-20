using FluentValidation;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.AddRoom;

internal sealed class AddRoomValidator : AbstractValidator<AddRoomRequest>
{
    public AddRoomValidator()
    {
        RuleFor(r => r.RoomName).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(100).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.RoomCapacity).GreaterThan(0).WithMessage(ValidationMessages.GreaterThanZero);
        RuleFor(r => r.RoomType).IsInEnum();
        RuleFor(r => r.RoomResources).MaximumLength(500).WithMessage(ValidationMessages.MaximumLength);
    }
}
