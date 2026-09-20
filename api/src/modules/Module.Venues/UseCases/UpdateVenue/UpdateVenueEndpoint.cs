using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.UpdateVenue;

internal sealed class UpdateVenueEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, UpdateVenueRequest request, IUseCase<UpdateVenueRequest, UpdateVenueResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { VenueId = id }, ct)).ToHttpResult())
            .WithName("UpdateVenue")
            .WithSummary("Atualiza os dados de um local")
            .WithDescription("Atualiza nome, descrição e endereço. Salas são gerenciadas em `/rooms`. **Perfil exigido:** Administrador ou Organizador.")
            .WithValidation<UpdateVenueRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<UpdateVenueResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
