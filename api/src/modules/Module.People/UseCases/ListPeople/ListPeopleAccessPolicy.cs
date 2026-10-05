using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.People.UseCases.ListPeople;

/// <summary>Somente administrador lista pessoas: a listagem expõe dados pessoais de terceiros.</summary>
internal sealed class ListPeopleAccessPolicy(ICurrentUser user) : IAccessPolicy<ListPeopleRequest>
{
    public Task<bool> CanExecuteAsync(ListPeopleRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.Id is not null && user.HasRole(DefaultRoles.Administrator));
}
