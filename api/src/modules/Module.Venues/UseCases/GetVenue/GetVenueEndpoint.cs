using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Venues.UseCases.GetVenue;

internal sealed class GetVenueEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, IUseCase<GetVenueRequest, GetVenueResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new GetVenueRequest(id), ct)).ToHttpResult())
            .WithName("GetVenue")
            .WithSummary("Obtém um local com suas salas")
            .WithDescription("Retorna o local, endereço, capacidade total (soma das salas ativas) e a lista de salas.")
            .Produces<GetVenueResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
