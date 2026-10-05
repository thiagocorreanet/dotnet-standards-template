using Shared.Contracts.Common;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.ListTracks;

/// <summary>Qualquer usuário autenticado com vínculo local consulta eventos e trilhas.</summary>
internal sealed class ListTracksAccessPolicy(ICurrentUser user) : IAccessPolicy<ListTracksRequest>
{
    public Task<bool> CanExecuteAsync(ListTracksRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.Id is not null);
}
