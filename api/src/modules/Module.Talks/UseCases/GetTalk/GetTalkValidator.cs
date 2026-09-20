using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.GetTalk;

internal sealed class GetTalkValidator : AbstractValidator<GetTalkRequest>
{
    public GetTalkValidator() => RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
