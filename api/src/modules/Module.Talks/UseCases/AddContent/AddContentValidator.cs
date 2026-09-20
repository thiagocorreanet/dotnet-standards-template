using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.AddContent;

internal sealed class AddContentValidator : AbstractValidator<AddContentRequest>
{
    public AddContentValidator()
    {
        RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.ContentTitle).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(200).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.ContentType).IsInEnum();
        RuleFor(r => r.ContentUrl).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(2000).WithMessage(ValidationMessages.MaximumLength)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _)).When(r => !string.IsNullOrWhiteSpace(r.ContentUrl)).WithMessage("O campo {PropertyName} deve ser uma URL absoluta válida.");
        RuleFor(r => r.ContentDescription).MaximumLength(1000).WithMessage(ValidationMessages.MaximumLength);
    }
}
