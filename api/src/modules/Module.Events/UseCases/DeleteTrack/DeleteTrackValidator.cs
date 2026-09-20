using FluentValidation;
using Shared.Http.Validation;
namespace Module.Events.UseCases.DeleteTrack;
internal sealed class DeleteTrackValidator : AbstractValidator<DeleteTrackRequest>
{
    public DeleteTrackValidator() { RuleFor(x => x.EventId).NotEmpty().WithMessage(ValidationMessages.GuidRequired); RuleFor(x => x.TrackId).NotEmpty().WithMessage(ValidationMessages.GuidRequired); }
}
