using FluentValidation;
using Shared.Http.Validation;
namespace Module.Events.UseCases.AddTrack;
internal sealed class AddTrackValidator : AbstractValidator<AddTrackRequest>
{
    public AddTrackValidator()
    {
        RuleFor(x => x.TrackName).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(120).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(x => x.TrackDescription).MaximumLength(1000).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(x => x.TrackColor).Matches("^#[0-9A-Fa-f]{6}$").When(x => !string.IsNullOrWhiteSpace(x.TrackColor)).WithMessage("Informe uma cor hexadecimal no formato #RRGGBB.");
    }
}
