using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.People.UseCases.UpdatePerson;

internal sealed class UpdatePersonEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, UpdatePersonRequest request, IUseCase<UpdatePersonRequest, UpdatePersonResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { PersonId = id }, ct)).ToHttpResult())
            .WithName("UpdatePerson")
            .WithSummary("Atualiza os dados de uma pessoa")
            .WithDescription("""
                Substitui todos os dados da pessoa (PUT), inclusive `isActive` (pessoas inativas não são retornadas via `IPeopleModuleApi`).

                As mesmas regras do cadastro se aplicam: e-mail normalizado em minúsculas e CPF só com dígitos, ambos únicos entre pessoas ativas.

                **Erros:** `400 Validation` · `404 People.PersonNotFound` · `409 People.EmailAlreadyRegistered` · `409 People.DocumentAlreadyRegistered`.

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<UpdatePersonRequest>()
            .RequireAuthorization()
            .Produces<UpdatePersonResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
