using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.IssueCertificate;

internal sealed class IssueCertificateValidator : AbstractValidator<IssueCertificateRequest>
{
    public IssueCertificateValidator()
    {
        RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.PersonId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
    }
}
