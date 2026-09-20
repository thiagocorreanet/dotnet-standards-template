using FluentValidation;
using Module.People.Domain;
using Shared.Http.Validation;

namespace Module.People.UseCases.CreatePerson;

internal sealed class CreatePersonValidator : AbstractValidator<CreatePersonRequest>
{
    public CreatePersonValidator()
    {
        RuleFor(r => r.PersonName).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(150).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.PersonEmail).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(200).WithMessage(ValidationMessages.MaximumLength)
            .EmailAddress().WithMessage(ValidationMessages.EmailInvalid);
        RuleFor(r => r.PersonPhone).MaximumLength(20).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.PersonDocument).Must(Cpf.IsValid).When(r => !string.IsNullOrWhiteSpace(r.PersonDocument)).WithMessage(PeopleMessages.CpfInvalid);
        RuleFor(r => r.PersonCompany).MaximumLength(150).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.PersonJobTitle).MaximumLength(100).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.PersonShortBio).MaximumLength(2000).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.PersonPhotoUrl).MaximumLength(500).WithMessage(ValidationMessages.MaximumLength)
            .Must(PeopleMessages.IsHttpUrl).When(r => !string.IsNullOrWhiteSpace(r.PersonPhotoUrl)).WithMessage(PeopleMessages.UrlInvalid);
    }
}

/// <summary>Mensagens e regras de validação específicas do módulo Pessoas, compartilhadas entre os validators.</summary>
internal static class PeopleMessages
{
    public const string CpfInvalid = "O campo {PropertyName} deve ser um CPF válido (11 dígitos, com ou sem máscara).";
    public const string UrlInvalid = "O campo {PropertyName} deve ser uma URL absoluta http(s).";

    public static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
