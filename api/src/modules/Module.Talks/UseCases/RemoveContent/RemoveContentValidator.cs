using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.RemoveContent;

internal sealed class RemoveContentValidator : AbstractValidator<RemoveContentRequest>
{
    public RemoveContentValidator()
    {
        RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.ContentId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
    }
}
