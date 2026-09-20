using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.DeleteTalk;

internal sealed class DeleteTalkValidator : AbstractValidator<DeleteTalkRequest>
{
    public DeleteTalkValidator() => RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
