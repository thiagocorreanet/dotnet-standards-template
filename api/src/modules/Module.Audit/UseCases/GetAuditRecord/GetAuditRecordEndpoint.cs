using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Audit.UseCases.GetAuditRecord;

internal sealed class GetAuditRecordEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/records/{id:guid}", async (Guid id, IUseCase<GetAuditRecordRequest, GetAuditRecordResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new GetAuditRecordRequest(id), ct)).ToHttpResult())
            .WithName("GetAuditRecord")
            .WithSummary("Obtém o detalhe de um registro de auditoria")
            .WithDescription("""
                Retorna o registro completo, incluindo `previousData` e `newData` como **strings JSON**:

                - `Insert`: `previousData` nulo; `newData` com todas as propriedades da entidade.
                - `Update`/`Delete`: apenas as propriedades modificadas, antes e depois.

                `traceId` permite correlacionar com logs e traces da requisição original. **Erros:** `404 Audit.RecordNotFound`. **Perfil exigido:** Administrador.
                """)
            .RequireAuthorization(Policies.Administration)
            .Produces<GetAuditRecordResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
