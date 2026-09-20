using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Common;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Venues.UseCases.ListVenues;

internal sealed class ListVenuesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("", async ([AsParameters] ListVenuesRequest request, IUseCase<ListVenuesRequest, PagedResult<ListVenuesItemResponse>> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToHttpResult())
            .WithName("ListVenues")
            .WithSummary("Lista locais (paginado)")
            .WithDescription("""
                Lista locais ordenados por nome, com filtros opcionais:

                | Parâmetro | Descrição |
                |-----------|-----------|
                | `search` | Texto contido no nome ou na cidade (case-insensitive) |
                | `addressState` | UF com 2 letras |
                | `isActive` | `true`/`false` |
                | `page`, `pageSize` | Paginação (máx. 100 por página) |
                | `sortBy`, `direction` | Campo permitido e direção `Asc`/`Desc` |
                """)
            .WithValidation<ListVenuesRequest>()
            .Produces<PagedResult<ListVenuesItemResponse>>();
}
