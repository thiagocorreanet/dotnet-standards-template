using Shared.Contracts.Common;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.ListEvents;

/// <summary>Qualquer usuário autenticado com vínculo local consulta eventos e trilhas.</summary>
internal sealed class ListEventsAccessPolicy(ICurrentUser user) : IAccessPolicy<ListEventsRequest>
{
    public Task<bool> CanExecuteAsync(ListEventsRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.Id is not null);
}
