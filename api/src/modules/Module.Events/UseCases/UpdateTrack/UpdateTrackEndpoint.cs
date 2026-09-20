using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;
namespace Module.Events.UseCases.UpdateTrack;
internal sealed class UpdateTrackEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) => group.MapPut("/{id:guid}/tracks/{trackId:guid}", async (Guid id, Guid trackId, UpdateTrackRequest request, IUseCase<UpdateTrackRequest, UpdateTrackResponse> useCase, CancellationToken ct) => (await useCase.HandleAsync(request with { EventId = id, TrackId = trackId }, ct)).ToHttpResult())
        .WithName("UpdateTrack").WithSummary("Atualiza uma trilha do evento").WithDescription("Atualiza identificação e disponibilidade da trilha. **Perfil exigido:** Administrador ou Organizador.").WithValidation<UpdateTrackRequest>().RequireAuthorization(Policies.Management).Produces<UpdateTrackResponse>().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
}
