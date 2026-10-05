using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.AddRoom;

/// <summary>Somente administrador altera locais e salas.</summary>
internal sealed class AddRoomAccessPolicy(ICurrentUser user) : IAccessPolicy<AddRoomRequest>
{
    public Task<bool> CanExecuteAsync(AddRoomRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
