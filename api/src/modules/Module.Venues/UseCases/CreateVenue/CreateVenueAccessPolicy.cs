using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.CreateVenue;

/// <summary>Somente administrador altera locais e salas.</summary>
internal sealed class CreateVenueAccessPolicy(ICurrentUser user) : IAccessPolicy<CreateVenueRequest>
{
    public Task<bool> CanExecuteAsync(CreateVenueRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
