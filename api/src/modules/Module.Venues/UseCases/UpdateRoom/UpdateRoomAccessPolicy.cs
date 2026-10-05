using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.UpdateRoom;

/// <summary>Somente administrador altera locais e salas.</summary>
internal sealed class UpdateRoomAccessPolicy(ICurrentUser user) : IAccessPolicy<UpdateRoomRequest>
{
    public Task<bool> CanExecuteAsync(UpdateRoomRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
