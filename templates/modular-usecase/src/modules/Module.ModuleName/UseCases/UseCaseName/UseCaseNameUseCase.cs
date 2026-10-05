using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.ModuleName.UseCases.UseCaseName;

#if (command)
/// <summary>
/// Escrita: <c>[Command]</c> abre a transação e trava a chave do módulo antes de qualquer leitura. Use
/// <c>[Command("recurso:{Id}")]</c> só quando a invariante depender apenas daquele recurso.
/// </summary>
[Command]
#else
/// <summary>Consulta: sem <c>[Command]</c>, roda no escopo da requisição, sem transação nem lock.</summary>
#endif
internal sealed class UseCaseNameUseCase : IUseCase<UseCaseNameRequest, UseCaseNameResponse>
{
    // Injete ModuleNameDbContext e o que mais precisar; regra no Domain, falha de negócio como Error (sem exceção).
    public Task<Result<UseCaseNameResponse>> HandleAsync(UseCaseNameRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<UseCaseNameResponse>>(Error.Failure("ModuleName.NotImplemented", "Caso de uso ainda não implementado."));
}
