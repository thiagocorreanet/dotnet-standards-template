using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.People.UseCases.DeletePerson;

internal sealed class DeletePersonEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, IUseCase<DeletePersonRequest, DeletePersonResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new DeletePersonRequest(id), ct)).ToNoContentResult())
            .WithName("DeletePerson")
            .WithSummary("Exclui (logicamente) uma pessoa")
            .WithDescription("""
                Soft delete: o registro permanece no banco com `DeletedAt`/`DeletedBy` preenchidos e deixa de ser retornado.
                O e-mail e o CPF ficam liberados para um novo cadastro. Publica o evento de integração `PersonDeleted`.

                **Erros:** `404 People.PersonNotFound`. **Perfil exigido:** Administrador ou Organizador.
                """)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
