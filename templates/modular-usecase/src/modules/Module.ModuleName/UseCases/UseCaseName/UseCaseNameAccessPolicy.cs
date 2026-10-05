using Shared.Http.Endpoints;

namespace Module.ModuleName.UseCases.UseCaseName;

/// <summary>
/// Quem pode executar o caso de uso. Nasce negando tudo (falha fechada): escreva a regra antes de liberar o endpoint.
/// </summary>
/// <remarks>
/// Padrão da base, com <c>ICurrentUser user</c> injetado:
/// <code>
/// if (!user.IsAuthenticated || user.Id is null) return false;
/// if (user.HasRole(DefaultRoles.Administrator)) return true;
/// // regra de dono/contexto do recurso, consultada no servidor
/// </code>
/// Regra repetida entre policies vira serviço pequeno em <c>Module.ModuleName/Shared/</c>.
/// </remarks>
internal sealed class UseCaseNameAccessPolicy : IAccessPolicy<UseCaseNameRequest>
{
    public Task<bool> CanExecuteAsync(UseCaseNameRequest request, CancellationToken ct) => Task.FromResult(false);
}
