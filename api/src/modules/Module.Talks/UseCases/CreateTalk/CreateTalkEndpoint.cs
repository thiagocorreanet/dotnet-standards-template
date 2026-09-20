using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.CreateTalk;

internal sealed class CreateTalkEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("", async (CreateTalkRequest request, IUseCase<CreateTalkRequest, CreateTalkResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToCreatedResult(r => $"/api/v1/talks/{r.Id}"))
            .WithName("CreateTalk")
            .WithSummary("Cria uma palestra em um evento")
            .WithDescription("""
                Cria uma palestra vinculada a um evento, com ao menos um palestrante.

                **Regras validadas com outros módulos:**

                | Regra | Erro |
                |-------|------|
                | Evento existe | `422 Talks.EventNotFound` |
                | Evento não está `Closed`/`Canceled` | `422 Talks.EventDoesNotAcceptTalks` |
                | Período da palestra dentro do período do evento | `422 Talks.PeriodOutsideEvent` |
                | `roomId` (opcional) existe e está ativa | `422 Talks.RoomNotFound` |
                | Sala pertence ao local do evento | `422 Talks.RoomDoesNotBelongToVenue` |
                | Sem outra palestra na mesma sala no horário | `409 Talks.RoomOccupied` |
                | Todas as pessoas informadas existem | `422 Talks.PersonNotFound` |
                | Pessoa não repetida entre os palestrantes | `409 Talks.SpeakerAlreadyLinked` |

                Papéis de palestrante: `Principal`, `Coauthor`, `Moderator`. Emite o evento de integração `TalkCreated`.

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<CreateTalkRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<CreateTalkResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
