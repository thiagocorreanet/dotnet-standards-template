using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.AddRoom;

internal sealed class AddRoomEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/{id:guid}/rooms", async (Guid id, AddRoomRequest request, IUseCase<AddRoomRequest, AddRoomResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { VenueId = id }, ct)).ToCreatedResult(r => $"/api/v1/venues/{r.VenueId}/rooms/{r.Id}"))
            .WithName("AddRoom")
            .WithSummary("Adiciona uma sala (ambiente) a um local")
            .WithDescription("""
                Tipos de sala: `SingleRoom`, `Auditorium`, `Classroom`, `Laboratory`, `RecreationArea`, `Coworking`, `Other`.

                O nome da sala é único dentro do local (`409 Venues.DuplicateRoomName`). **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<AddRoomRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<AddRoomResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
