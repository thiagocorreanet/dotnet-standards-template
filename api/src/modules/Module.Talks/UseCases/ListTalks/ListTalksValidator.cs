using FluentValidation;
using Shared.Contracts.Common;

namespace Module.Talks.UseCases.ListTalks;

internal sealed class ListTalksValidator : AbstractValidator<ListTalksRequest>
{
    public ListTalksValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PagedRequest.MaximumPageSize);
        RuleFor(r => r.Search).MaximumLength(100);
        RuleFor(r => r.SortBy).Must(c => new[] { "talkTitle", "talkStart", "speakersCount", "attendancesCount" }.Contains(c!, StringComparer.OrdinalIgnoreCase)).When(r => !string.IsNullOrWhiteSpace(r.SortBy));
    }
}
