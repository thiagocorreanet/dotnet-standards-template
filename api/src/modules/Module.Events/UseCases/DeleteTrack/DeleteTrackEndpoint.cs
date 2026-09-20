using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
namespace Module.Events.UseCases.DeleteTrack;
internal sealed class DeleteTrackEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) => group.MapDelete("/{id:guid}/tracks/{trackId:guid}", async (Guid id, Guid trackId, IUseCase<DeleteTrackRequest, DeleteTrackResponse> useCase, CancellationToken ct) => (await useCase.HandleAsync(new(id, trackId), ct)).ToNoContentResult())
        .WithName("DeleteTrack").WithSummary("Exclui logicamente uma trilha").WithDescription("O evento deve manter ao menos uma trilha. **Perfil exigido:** Administrador ou Organizador.").RequireAuthorization(Policies.Management).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
