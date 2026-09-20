using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.AddSpeaker;

internal sealed class AddSpeakerEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/{id:guid}/speakers", async (Guid id, AddSpeakerRequest request, IUseCase<AddSpeakerRequest, AddSpeakerResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { TalkId = id }, ct)).ToCreatedResult(r => $"/api/v1/talks/{r.TalkId}"))
            .WithName("AddSpeaker")
            .WithSummary("Vincula uma pessoa como palestrante")
            .WithDescription("""
                Adiciona uma pessoa (módulo Pessoas) à palestra com o papel `Principal`, `Coauthor` ou `Moderator`.

                - A pessoa precisa existir (`422 Talks.PersonNotFound`).
                - Uma pessoa só é vinculada uma vez por palestra (`409 Talks.SpeakerAlreadyLinked`).

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<AddSpeakerRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<AddSpeakerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
