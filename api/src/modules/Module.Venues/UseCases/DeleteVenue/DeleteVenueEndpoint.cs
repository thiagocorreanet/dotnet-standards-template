using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.DeleteVenue;

internal sealed class DeleteVenueEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/{id:guid}", async (Guid id, IUseCase<DeleteVenueRequest, DeleteVenueResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new DeleteVenueRequest(id), ct)).ToNoContentResult())
            .WithName("DeleteVenue")
            .WithSummary("Exclui (logicamente) um local")
            .WithDescription("""
                Soft delete do local e de suas salas: registros permanecem no banco com `DeletedAt`/`DeletedBy` preenchidos.

                A exclusão é recusada enquanto houver referência ativa: um evento vinculado ao local ou uma palestra
                ocupando qualquer sala dele, inclusive palestra de evento já excluído.

                **Erros:** `404 Venues.VenueNotFound` · `409 Venues.ResourceInUse`.

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .RequireAuthorization(Policies.Management)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
