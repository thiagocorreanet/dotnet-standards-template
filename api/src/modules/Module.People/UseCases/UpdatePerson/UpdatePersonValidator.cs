using FluentValidation;
using Module.People.Domain;
using Module.People.UseCases.CreatePerson;
using Shared.Http.Validation;

namespace Module.People.UseCases.UpdatePerson;

internal sealed class UpdatePersonValidator : AbstractValidator<UpdatePersonRequest>
{
    public UpdatePersonValidator()
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
