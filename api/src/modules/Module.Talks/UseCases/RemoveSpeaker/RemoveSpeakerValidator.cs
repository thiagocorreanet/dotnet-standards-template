using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.RemoveSpeaker;

internal sealed class RemoveSpeakerValidator : AbstractValidator<RemoveSpeakerRequest>
{
    public RemoveSpeakerValidator()
    {
        RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.PersonId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
    }
}
