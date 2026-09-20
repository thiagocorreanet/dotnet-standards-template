using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.CreateTalk;

internal sealed class CreateTalkValidator : AbstractValidator<CreateTalkRequest>
{
    public CreateTalkValidator()
    {
        RuleFor(r => r.EventId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.TrackId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.RoomId).NotEqual(Guid.Empty).When(r => r.RoomId.HasValue).WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.TalkTitle).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(200).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.TalkDescription).MaximumLength(4000).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.TalkStart).NotEmpty().WithMessage(ValidationMessages.Required);
        RuleFor(r => r.TalkEnd).NotEmpty().WithMessage(ValidationMessages.Required)
            .GreaterThan(r => r.TalkStart).WithMessage("O campo {PropertyName} deve ser posterior ao início da palestra.");
        RuleFor(r => r.Speakers).NotEmpty().WithMessage("A palestra precisa de ao menos um palestrante.");
        RuleForEach(r => r.Speakers).ChildRules(p =>
        {
            p.RuleFor(x => x.PersonId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
            p.RuleFor(x => x.SpeakerRole).IsInEnum();
        });
    }
}
