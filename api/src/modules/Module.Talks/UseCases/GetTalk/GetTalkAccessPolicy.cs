using Shared.Contracts.Common;
using Shared.Http.Endpoints;

namespace Module.Talks.UseCases.GetTalk;

/// <summary>Qualquer usuário autenticado com vínculo local consulta palestras.</summary>
internal sealed class GetTalkAccessPolicy(ICurrentUser user) : IAccessPolicy<GetTalkRequest>
{
    public Task<bool> CanExecuteAsync(GetTalkRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.Id is not null);
}
