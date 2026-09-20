using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.DeleteEvent;

internal sealed class DeleteEventEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, IUseCase<DeleteEventRequest, DeleteEventResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new DeleteEventRequest(id), ct)).ToNoContentResult())
            .WithName("DeleteEvent")
            .WithSummary("Exclui (logicamente) um evento")
            .WithDescription("""
                Soft delete do evento e de suas inscrições: registros permanecem no banco com `DeletedAt`/`DeletedBy` preenchidos.

                Permitido somente em `Draft` ou `Canceled` (`422 Events.EventCannotBeDeleted`). **Perfil exigido:** Administrador ou Organizador.
                """)
            .RequireAuthorization(Policies.Management)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
