using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Events.UseCases.UpdateEvent;

internal sealed class UpdateEventEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, UpdateEventRequest request, IUseCase<UpdateEventRequest, UpdateEventResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { EventId = id }, ct)).ToHttpResult())
            .WithName("UpdateEvent")
            .WithSummary("Atualiza os dados de um evento")
            .WithDescription("""
                Atualiza nome, descrição, período, formato, local/link e capacidade. A situação é alterada apenas em `PATCH /{id}/status`.

                - Permitido somente em `Draft` ou `Published` (`422 Events.EventCannotBeUpdated`).
                - Mesmas regras de consistência de formato da criação; `venueId` precisa existir (`422 Events.VenueNotFound`).

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<UpdateEventRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<UpdateEventResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
