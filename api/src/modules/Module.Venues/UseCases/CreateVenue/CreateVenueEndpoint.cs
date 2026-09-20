using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.CreateVenue;

internal sealed class CreateVenueEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("", async (CreateVenueRequest request, IUseCase<CreateVenueRequest, CreateVenueResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToCreatedResult(r => $"/api/v1/venues/{r.Id}"))
            .WithName("CreateVenue")
            .WithSummary("Cria um local")
            .WithDescription("""
                Cria um local para eventos presenciais.

                - Se `singleRoomCapacity` for informado, o local nasce com a sala **"Ambiente único"** (regra: local de um ambiente = uma sala).
                - Caso contrário, adicione salas em `POST /api/v1/venues/{id}/rooms`.
                - O nome do local é único entre os locais ativos (`409 Venues.DuplicateVenueName`).

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<CreateVenueRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<CreateVenueResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
