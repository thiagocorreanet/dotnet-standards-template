using Shared.Contracts.Common;
using Shared.Http.Endpoints;

namespace Module.Venues.UseCases.ListRooms;

/// <summary>Qualquer usuário autenticado consulta locais e salas.</summary>
internal sealed class ListRoomsAccessPolicy(ICurrentUser user) : IAccessPolicy<ListRoomsRequest>
{
    public Task<bool> CanExecuteAsync(ListRoomsRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated);
}
