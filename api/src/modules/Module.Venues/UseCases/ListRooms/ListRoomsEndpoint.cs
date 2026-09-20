using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.ListRooms;

internal sealed class ListRoomsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{id:guid}/rooms", async ([AsParameters] ListRoomsRequest request, IUseCase<ListRoomsRequest, IReadOnlyList<ListRoomsItemResponse>> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToHttpResult())
            .WithName("ListRooms")
            .WithSummary("Lista as salas de um local")
            .WithDescription("Salas ordenadas por nome. Use `isActive=true` para obter apenas salas disponíveis para alocação.")
            .WithValidation<ListRoomsRequest>()
            .Produces<IReadOnlyList<ListRoomsItemResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
