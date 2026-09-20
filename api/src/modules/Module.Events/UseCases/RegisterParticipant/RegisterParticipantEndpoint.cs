using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Events.UseCases.RegisterParticipant;

internal sealed class RegisterParticipantEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/{id:guid}/registrations", async (Guid id, RegisterParticipantRequest request, IUseCase<RegisterParticipantRequest, RegisterParticipantResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { EventId = id }, ct)).ToCreatedResult(r => $"/api/v1/events/{r.EventId}/registrations/{r.Id}"))
            .WithName("RegisterParticipant")
            .WithSummary("Inscreve uma pessoa em um evento")
            .WithDescription("""
                Cria uma inscrição **Confirmada** e emite `RegistrationCompleted`.

                | Regra | Erro |
                |-------|------|
                | Evento deve estar `Published` ou `InProgress` | `422 Events.EventDoesNotAcceptRegistrations` |
                | Pessoa deve existir no módulo Pessoas | `422 Events.PersonNotFound` |
                | Pessoa sem inscrição confirmada prévia no evento | `409 Events.PersonAlreadyRegistered` |
                | Capacidade (`eventMaximumCapacity` ou, se nula e há local, a capacidade total do local) não atingida | `422 Events.CapacityExhausted` |

                **Perfil exigido:** qualquer usuário autenticado.
                """)
            .WithValidation<RegisterParticipantRequest>()
            .Produces<RegisterParticipantResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
