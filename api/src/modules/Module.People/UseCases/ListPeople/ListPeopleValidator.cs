using FluentValidation;
using Shared.Contracts.Common;

namespace Module.People.UseCases.ListPeople;

internal sealed class ListPeopleValidator : AbstractValidator<ListPeopleRequest>
{
    public ListPeopleValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PagedRequest.MaximumPageSize);
        RuleFor(r => r.Search).MaximumLength(100);
        RuleFor(r => r.SortBy).Must(c => new[] { "personName", "personEmail", "personCompany", "isActive" }.Contains(c!, StringComparer.OrdinalIgnoreCase)).When(r => !string.IsNullOrWhiteSpace(r.SortBy));
    }
}
