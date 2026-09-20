using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Common;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.People.UseCases.ListPeople;

internal sealed class ListPeopleEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("", async ([AsParameters] ListPeopleRequest request, IUseCase<ListPeopleRequest, PagedResult<ListPeopleItemResponse>> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToHttpResult())
            .WithName("ListPeople")
            .WithSummary("Lista pessoas (paginado)")
            .WithDescription("""
                Lista pessoas ordenadas por nome, com filtros opcionais:

                | Parâmetro | Descrição |
                |-----------|-----------|
                | `search` | Texto contido no nome, no e-mail ou na empresa (case-insensitive, até 100 caracteres) |
                | `isActive` | `true`/`false` |
                | `page`, `pageSize` | Paginação (máx. 100 por página) |
                | `sortBy`, `direction` | Campo permitido e direção `Asc`/`Desc` |

                Pessoas excluídas nunca são retornadas. **Perfil exigido:** qualquer usuário autenticado.
                """)
            .WithValidation<ListPeopleRequest>()
            .Produces<PagedResult<ListPeopleItemResponse>>();
}
