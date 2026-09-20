using FluentValidation;
using Module.Events.UseCases.CreateEvent;
using Shared.Contracts.Common;

namespace Module.Events.UseCases.ListEvents;

internal sealed class ListEventsValidator : AbstractValidator<ListEventsRequest>
{
    public ListEventsValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PagedRequest.MaximumPageSize);
        RuleFor(r => r.Search).MaximumLength(100);
        RuleFor(r => r.SortBy).Must(c => new[] { "eventName", "eventStartDate", "eventFormat", "eventStatus", "confirmedRegistrations" }.Contains(c!, StringComparer.OrdinalIgnoreCase)).When(r => !string.IsNullOrWhiteSpace(r.SortBy));
        RuleFor(r => r.EventStatus).IsInEnum().When(r => r.EventStatus.HasValue);
        RuleFor(r => r.EventFormat).IsInEnum().When(r => r.EventFormat.HasValue).WithMessage(EventValidationMessages.InvalidFormat);
        RuleFor(r => r.StartDateUntil).GreaterThanOrEqualTo(r => r.StartDateFrom!.Value)
            .When(r => r.StartDateFrom.HasValue && r.StartDateUntil.HasValue)
            .WithMessage("O campo {PropertyName} deve ser igual ou posterior a DataInicioDe.");
    }
}
