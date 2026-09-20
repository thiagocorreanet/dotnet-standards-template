using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.UpdateTalk;

internal sealed class UpdateTalkEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, UpdateTalkRequest request, IUseCase<UpdateTalkRequest, UpdateTalkResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { TalkId = id }, ct)).ToHttpResult())
            .WithName("UpdateTalk")
            .WithSummary("Atualiza os dados de uma palestra")
            .WithDescription("""
                Atualiza título, descrição, sala e horário. O evento da palestra não muda; palestrantes e conteúdos são gerenciados em `/speakers` e `/contents`.

                As mesmas regras da criação são reaplicadas: evento ainda aceita palestras (`422 Talks.EventDoesNotAcceptTalks`),
                período dentro do evento (`422 Talks.PeriodOutsideEvent`), sala do local do evento (`422 Talks.RoomDoesNotBelongToVenue`)
                e sem sobreposição com **outras** palestras na sala (`409 Talks.RoomOccupied`).

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<UpdateTalkRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<UpdateTalkResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
