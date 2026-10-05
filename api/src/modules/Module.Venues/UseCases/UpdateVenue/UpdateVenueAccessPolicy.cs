using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.UpdateVenue;

/// <summary>Somente administrador altera locais e salas.</summary>
internal sealed class UpdateVenueAccessPolicy(ICurrentUser user) : IAccessPolicy<UpdateVenueRequest>
{
    public Task<bool> CanExecuteAsync(UpdateVenueRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
