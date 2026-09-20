using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.DeleteTalk;

internal sealed class DeleteTalkEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, IUseCase<DeleteTalkRequest, DeleteTalkResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new DeleteTalkRequest(id), ct)).ToNoContentResult())
            .WithName("DeleteTalk")
            .WithSummary("Exclui (logicamente) uma palestra")
            .WithDescription("""
                Soft delete da palestra e, em cascata lógica, de seus **palestrantes** e **conteúdos** (`DeletedAt`/`DeletedBy` preenchidos).

                **Presenças e certificados permanecem**: certificados já emitidos continuam validáveis em `GET /api/v1/talks/certificates/{code}`.

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .RequireAuthorization(Policies.Management)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
