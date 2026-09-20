using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.RecordAttendance;

internal sealed class RecordAttendanceValidator : AbstractValidator<RecordAttendanceRequest>
{
    public RecordAttendanceValidator()
    {
        RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
        RuleFor(r => r.PersonId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
    }
}
