using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.DeleteRoom;

/// <summary>Somente administrador altera locais e salas.</summary>
internal sealed class DeleteRoomAccessPolicy(ICurrentUser user) : IAccessPolicy<DeleteRoomRequest>
{
    public Task<bool> CanExecuteAsync(DeleteRoomRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
