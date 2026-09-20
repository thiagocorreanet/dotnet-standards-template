using FluentValidation;
using Module.Talks.Domain;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.ValidateCertificate;

internal sealed class ValidateCertificateValidator : AbstractValidator<ValidateCertificateRequest>
{
    public ValidateCertificateValidator() =>
        RuleFor(r => r.CertificateCode).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(CertificateCodeGenerator.Size * 2).WithMessage(ValidationMessages.MaximumLength);
}
