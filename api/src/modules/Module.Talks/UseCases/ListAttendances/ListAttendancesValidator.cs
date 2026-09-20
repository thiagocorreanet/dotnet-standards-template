using FluentValidation;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.ListAttendances;

internal sealed class ListAttendancesValidator : AbstractValidator<ListAttendancesRequest>
{
    public ListAttendancesValidator() => RuleFor(r => r.TalkId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
