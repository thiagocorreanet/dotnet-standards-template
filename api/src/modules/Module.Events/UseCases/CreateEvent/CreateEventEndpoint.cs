using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Events.UseCases.CreateEvent;

internal sealed class CreateEventEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("", async (CreateEventRequest request, IUseCase<CreateEventRequest, CreateEventResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToCreatedResult(r => $"/api/v1/events/{r.Id}"))
            .WithName("CreateEvent")
            .WithSummary("Cria um evento (em rascunho)")
            .WithDescription("""
                Cria um evento na situação **Rascunho**. Publique-o depois em `PATCH /api/v1/events/{id}/status`.

                | Formato | `venueId` | `eventRemoteUrl` |
                |---------|-----------|--------------------|
                | `InPerson` | obrigatório | — |
                | `Remote` | não permitido | obrigatório |
                | `Hybrid` | obrigatório | obrigatório |

                - `eventEndDate` deve ser posterior a `eventStartDate`.
                - `venueId`, quando informado, precisa existir no módulo Locais (`422 Events.VenueNotFound`).
                - `eventMaximumCapacity` é opcional; se omitida em evento com local, vale a capacidade total do local nas inscrições.

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<CreateEventRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<CreateEventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
