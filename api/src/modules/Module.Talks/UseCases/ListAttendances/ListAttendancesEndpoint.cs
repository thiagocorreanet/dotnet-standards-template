using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.ListAttendances;

internal sealed class ListAttendancesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{id:guid}/attendances", async (Guid id, IUseCase<ListAttendancesRequest, IReadOnlyList<ListAttendancesItemResponse>> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new ListAttendancesRequest(id), ct)).ToHttpResult())
            .WithName("ListAttendances")
            .WithSummary("Lista as presenças registradas na palestra")
            .WithDescription("Retorna as presenças em ordem de registro, com o nome da pessoa (módulo Pessoas) e se o certificado já foi emitido (`certificateIssued`).")
            .Produces<IReadOnlyList<ListAttendancesItemResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
