using FluentValidation;
using Shared.Http.Validation;

namespace Module.Audit.UseCases.GetAuditRecord;

internal sealed class GetAuditRecordValidator : AbstractValidator<GetAuditRecordRequest>
{
    public GetAuditRecordValidator() => RuleFor(r => r.RecordId).NotEmpty().WithMessage(ValidationMessages.GuidRequired);
}
