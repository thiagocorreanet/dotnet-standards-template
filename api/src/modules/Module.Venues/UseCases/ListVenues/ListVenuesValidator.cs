using FluentValidation;
using Shared.Contracts.Common;

namespace Module.Venues.UseCases.ListVenues;

internal sealed class ListVenuesValidator : AbstractValidator<ListVenuesRequest>
{
    public ListVenuesValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PagedRequest.MaximumPageSize);
        RuleFor(r => r.AddressState).Length(2).When(r => !string.IsNullOrWhiteSpace(r.AddressState));
        RuleFor(r => r.Search).MaximumLength(100);
        RuleFor(r => r.SortBy).Must(c => new[] { "venueName", "addressCity", "roomsCount", "venueTotalCapacity" }.Contains(c!, StringComparer.OrdinalIgnoreCase)).When(r => !string.IsNullOrWhiteSpace(r.SortBy));
    }
}
