using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Common;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.ListTalks;

internal sealed class ListTalksEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("", async ([AsParameters] ListTalksRequest request, IUseCase<ListTalksRequest, PagedResult<ListTalksItemResponse>> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToHttpResult())
            .WithName("ListTalks")
            .WithSummary("Lista palestras (paginado)")
            .WithDescription("""
                Lista palestras ordenadas por `talkStart`, com filtros opcionais:

                | Parâmetro | Descrição |
                |-----------|-----------|
                | `eventId` | Somente palestras do evento |
                | `search` | Texto contido no título (case-insensitive) |
                | `page`, `pageSize` | Paginação (máx. 100 por página) |
                | `sortBy`, `direction` | Campo permitido e direção `Asc`/`Desc` |
                """)
            .WithValidation<ListTalksRequest>()
            .Produces<PagedResult<ListTalksItemResponse>>();
}
