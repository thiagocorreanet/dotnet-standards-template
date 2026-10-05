using Shared.Contracts.Common;
using Shared.Http.Endpoints;

namespace Module.Talks.UseCases.ListTalks;

/// <summary>Qualquer usuário autenticado com vínculo local consulta palestras.</summary>
internal sealed class ListTalksAccessPolicy(ICurrentUser user) : IAccessPolicy<ListTalksRequest>
{
    public Task<bool> CanExecuteAsync(ListTalksRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.Id is not null);
}
