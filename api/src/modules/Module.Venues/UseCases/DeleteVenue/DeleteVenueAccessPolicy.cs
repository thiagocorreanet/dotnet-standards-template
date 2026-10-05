using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.DeleteVenue;

/// <summary>Somente administrador altera locais e salas.</summary>
internal sealed class DeleteVenueAccessPolicy(ICurrentUser user) : IAccessPolicy<DeleteVenueRequest>
{
    public Task<bool> CanExecuteAsync(DeleteVenueRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
