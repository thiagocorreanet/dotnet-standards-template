using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.DeleteRoom;

internal sealed class DeleteRoomEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/{id:guid}/rooms/{roomId:guid}", async (Guid id, Guid roomId, IUseCase<DeleteRoomRequest, DeleteRoomResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new DeleteRoomRequest(id, roomId), ct)).ToNoContentResult())
            .WithName("DeleteRoom")
            .WithSummary("Exclui (logicamente) uma sala")
            .WithDescription("Um local precisa manter ao menos uma sala (`422 Venues.VenueRequiresRoom`). **Perfil exigido:** Administrador ou Organizador.")
            .RequireAuthorization(Policies.Management)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
