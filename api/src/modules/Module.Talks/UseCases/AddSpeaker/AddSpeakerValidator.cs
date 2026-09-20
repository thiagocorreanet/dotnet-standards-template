using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.AddSpeaker;

internal sealed class AddSpeakerValidator : AbstractValidator<AddSpeakerRequest>
{
    public AddSpeakerValidator()
    {
        RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.PersonId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.SpeakerRole).IsInEnum();
    }
}
