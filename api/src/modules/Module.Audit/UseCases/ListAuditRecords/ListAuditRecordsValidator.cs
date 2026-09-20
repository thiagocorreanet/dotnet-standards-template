using FluentValidation;
using Shared.Contracts.Audit;
using Shared.Contracts.Common;
using Shared.Http.Validation;

namespace Module.Audit.UseCases.ListAuditRecords;

internal sealed class ListAuditRecordsValidator : AbstractValidator<ListAuditRecordsRequest>
{
    private static readonly string[] Operations = [AuditOperations.Insert, AuditOperations.Update, AuditOperations.Delete];

    public ListAuditRecordsValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PagedRequest.MaximumPageSize);
        RuleFor(r => r.Module).MaximumLength(50).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.EntityName).MaximumLength(100).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.EntityId).MaximumLength(100).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.SortBy).Must(c => new[] { "module", "entityName", "operation", "userName", "occurredOn" }.Contains(c!, StringComparer.OrdinalIgnoreCase)).When(r => !string.IsNullOrWhiteSpace(r.SortBy));
        RuleFor(r => r.Operation)
            .Must(o => Operations.Contains(o!.Trim(), StringComparer.OrdinalIgnoreCase))
            .When(r => !string.IsNullOrWhiteSpace(r.Operation))
            .WithMessage($"O campo {{PropertyName}} deve ser um de: {string.Join(", ", Operations)}.");
        RuleFor(r => r.OccurredUntil)
            .GreaterThanOrEqualTo(r => r.OccurredFrom)
            .When(r => r.OccurredFrom.HasValue && r.OccurredUntil.HasValue)
            .WithMessage("O campo {PropertyName} deve ser maior ou igual a OccurredFrom.");
    }
}
