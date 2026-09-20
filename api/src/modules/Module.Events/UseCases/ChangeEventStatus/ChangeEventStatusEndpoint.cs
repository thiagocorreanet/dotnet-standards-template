using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Events.UseCases.ChangeEventStatus;

internal sealed class ChangeEventStatusEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/{id:guid}/status", async (Guid id, ChangeEventStatusRequest request, IUseCase<ChangeEventStatusRequest, ChangeEventStatusResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { EventId = id }, ct)).ToHttpResult())
            .WithName("ChangeEventStatus")
            .WithSummary("Altera a situação do evento (publicar, iniciar, encerrar, cancelar)")
            .WithDescription("""
                Aplica uma transição na máquina de estados do evento.

                | De | Para | Regra |
                |----|------|-------|
                | `Draft` | `Published` | Exige ao menos uma palestra cadastrada (`422 Events.EventWithoutTalks`). Emite `EventPublished`. |
                | `Published` | `InProgress` | — |
                | `Published`, `InProgress` | `Closed` | — |
                | `Draft`, `Published`, `InProgress` | `Canceled` | `reason` obrigatório (`422 Events.CancellationReasonRequired`). Emite `EventCanceled`. |

                Qualquer outra combinação (inclusive voltar para `Draft`) responde `422 Events.InvalidStatusTransition`.

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<ChangeEventStatusRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<ChangeEventStatusResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
