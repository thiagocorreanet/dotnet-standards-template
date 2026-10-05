using Shared.Contracts.Common;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.GetEvent;

/// <summary>Qualquer usuário autenticado com vínculo local consulta eventos e trilhas.</summary>
internal sealed class GetEventAccessPolicy(ICurrentUser user) : IAccessPolicy<GetEventRequest>
{
    public Task<bool> CanExecuteAsync(GetEventRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.Id is not null);
}
