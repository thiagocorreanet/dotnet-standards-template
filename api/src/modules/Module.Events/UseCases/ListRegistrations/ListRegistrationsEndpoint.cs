using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Common;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Events.UseCases.ListRegistrations;

internal sealed class ListRegistrationsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{id:guid}/registrations", async (Guid id, [AsParameters] ListRegistrationsRequest request, IUseCase<ListRegistrationsRequest, PagedResult<ListRegistrationsItemResponse>> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { EventId = id }, ct)).ToHttpResult())
            .WithName("ListRegistrations")
            .WithSummary("Lista as inscrições de um evento (paginado)")
            .WithDescription("""
                Lista inscrições ordenadas pela data de realização, com nome e e-mail obtidos do módulo Pessoas em lote.

                | Parâmetro | Descrição |
                |-----------|-----------|
                | `registrationStatus` | `Confirmed` ou `Canceled` (omitido: todas) |
                | `page`, `pageSize` | Paginação (máx. 100 por página) |

                **Perfil exigido:** qualquer usuário autenticado.
                """)
            .WithValidation<ListRegistrationsRequest>()
            .Produces<PagedResult<ListRegistrationsItemResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
