using Shared.Contracts.Common;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.GetVenue;

/// <summary>Qualquer usuário autenticado consulta locais e salas.</summary>
internal sealed class GetVenueAccessPolicy(ICurrentUser user) : IAccessPolicy<GetVenueRequest>
{
    public Task<bool> CanExecuteAsync(GetVenueRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated);
}
