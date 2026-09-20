using FluentValidation;
using Shared.Contracts.Common;

namespace Module.Events.UseCases.ListRegistrations;

internal sealed class ListRegistrationsValidator : AbstractValidator<ListRegistrationsRequest>
{
    public ListRegistrationsValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, PagedRequest.MaximumPageSize);
        RuleFor(r => r.RegistrationStatus).IsInEnum().When(r => r.RegistrationStatus.HasValue);
    }
}
