using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Common;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Events.UseCases.ListEvents;

internal sealed class ListEventsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("", async ([AsParameters] ListEventsRequest request, IUseCase<ListEventsRequest, PagedResult<ListEventsItemResponse>> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToHttpResult())
            .WithName("ListEvents")
            .WithSummary("Lista eventos (paginado)")
            .WithDescription("""
                Lista eventos ordenados por data de início (mais recentes primeiro), com filtros opcionais:

                | Parâmetro | Descrição |
                |-----------|-----------|
                | `search` | Texto contido no nome ou na descrição (case-insensitive) |
                | `eventStatus` | `Draft`, `Published`, `InProgress`, `Closed` ou `Canceled` |
                | `eventFormat` | `InPerson`, `Remote` ou `Hybrid` |
                | `startDateFrom`, `startDateUntil` | Intervalo (ISO-8601) aplicado sobre `eventStartDate` |
                | `page`, `pageSize` | Paginação (máx. 100 por página) |
                | `sortBy`, `direction` | Campo permitido e direção `Asc`/`Desc` |

                Cada item traz `confirmedRegistrations`. **Perfil exigido:** qualquer usuário autenticado.
                """)
            .WithValidation<ListEventsRequest>()
            .Produces<PagedResult<ListEventsItemResponse>>();
}
