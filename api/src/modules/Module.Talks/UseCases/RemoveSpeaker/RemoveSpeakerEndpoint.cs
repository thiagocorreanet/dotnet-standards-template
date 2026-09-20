using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.RemoveSpeaker;

internal sealed class RemoveSpeakerEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/{id:guid}/speakers/{personId:guid}", async (Guid id, Guid personId, IUseCase<RemoveSpeakerRequest, RemoveSpeakerResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new RemoveSpeakerRequest(id, personId), ct)).ToNoContentResult())
            .WithName("RemoveSpeaker")
            .WithSummary("Remove (logicamente) um palestrante da palestra")
            .WithDescription("""
                Desvincula a pessoa da palestra (soft delete do vínculo).

                - Vínculo inexistente: `404 Talks.SpeakerNotFound`.
                - Uma palestra precisa manter ao menos um palestrante: `422 Talks.TalkRequiresSpeaker`.

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .RequireAuthorization(Policies.Management)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
