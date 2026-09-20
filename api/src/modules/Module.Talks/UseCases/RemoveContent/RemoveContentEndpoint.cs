using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.RemoveContent;

internal sealed class RemoveContentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/{id:guid}/contents/{contentId:guid}", async (Guid id, Guid contentId, IUseCase<RemoveContentRequest, RemoveContentResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new RemoveContentRequest(id, contentId), ct)).ToNoContentResult())
            .WithName("RemoveContent")
            .WithSummary("Remove (logicamente) um conteúdo da palestra")
            .WithDescription("Soft delete do conteúdo. Conteúdo inexistente na palestra: `404 Talks.ContentNotFound`. **Perfil exigido:** Administrador ou Organizador.")
            .RequireAuthorization(Policies.Management)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
