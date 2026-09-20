using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.UpdateRoom;

internal sealed class UpdateRoomEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPut("/{id:guid}/rooms/{roomId:guid}", async (Guid id, Guid roomId, UpdateRoomRequest request, IUseCase<UpdateRoomRequest, UpdateRoomResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { VenueId = id, RoomId = roomId }, ct)).ToHttpResult())
            .WithName("UpdateRoom")
            .WithSummary("Atualiza uma sala")
            .WithDescription("Atualiza nome, capacidade, tipo, recursos e disponibilidade (`isActive=false` torna a sala indisponível para novas palestras). **Perfil exigido:** Administrador ou Organizador.")
            .WithValidation<UpdateRoomRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<UpdateRoomResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
