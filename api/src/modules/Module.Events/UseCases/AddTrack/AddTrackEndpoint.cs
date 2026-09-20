using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;
namespace Module.Events.UseCases.AddTrack;
internal sealed class AddTrackEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) => group.MapPost("/{id:guid}/tracks", async (Guid id, AddTrackRequest request, IUseCase<AddTrackRequest, AddTrackResponse> useCase, CancellationToken ct) => (await useCase.HandleAsync(request with { EventId = id }, ct)).ToCreatedResult(r => $"/api/v1/events/{r.EventId}/tracks/{r.Id}"))
        .WithName("AddTrack").WithSummary("Adiciona uma trilha temática ao evento").WithDescription("O nome da trilha é único no evento. **Perfil exigido:** Administrador ou Organizador.").WithValidation<AddTrackRequest>().RequireAuthorization(Policies.Management).Produces<AddTrackResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
}
