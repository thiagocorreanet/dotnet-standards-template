using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.GetEvent;

internal sealed class GetEventEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, IUseCase<GetEventRequest, GetEventResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new GetEventRequest(id), ct)).ToHttpResult())
            .WithName("GetEvent")
            .WithSummary("Obtém um evento")
            .WithDescription("""
                Retorna os dados do evento, a quantidade de inscrições confirmadas e, quando há local, o `venueName` (obtido do módulo Locais).

                **Perfil exigido:** qualquer usuário autenticado.
                """)
            .Produces<GetEventResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
