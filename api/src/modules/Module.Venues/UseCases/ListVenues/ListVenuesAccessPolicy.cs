using Shared.Contracts.Common;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.ListVenues;

/// <summary>Qualquer usuário autenticado consulta locais e salas.</summary>
internal sealed class ListVenuesAccessPolicy(ICurrentUser user) : IAccessPolicy<ListVenuesRequest>
{
    public Task<bool> CanExecuteAsync(ListVenuesRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated);
}
