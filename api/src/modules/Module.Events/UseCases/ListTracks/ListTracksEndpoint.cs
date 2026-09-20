using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;
namespace Module.Events.UseCases.ListTracks;
internal sealed class ListTracksEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) => group.MapGet("/{id:guid}/tracks", async ([AsParameters] ListTracksRequest request, IUseCase<ListTracksRequest, IReadOnlyList<ListTracksItemResponse>> useCase, CancellationToken ct) => (await useCase.HandleAsync(request, ct)).ToHttpResult())
        .WithName("ListTracks").WithSummary("Lista as trilhas de um evento").WithDescription("Retorna as trilhas temáticas ordenadas por nome; use `isActive=true` para seleção em palestras.").WithValidation<ListTracksRequest>().Produces<IReadOnlyList<ListTracksItemResponse>>().ProducesProblem(StatusCodes.Status404NotFound);
}
