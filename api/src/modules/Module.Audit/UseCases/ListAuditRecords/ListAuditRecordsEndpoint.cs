using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Audit.UseCases.ListAuditRecords;

internal sealed class ListAuditRecordsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/records", async ([AsParameters] ListAuditRecordsRequest request, IUseCase<ListAuditRecordsRequest, PagedResult<ListAuditRecordsItemResponse>> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToHttpResult())
            .WithName("ListAuditRecords")
            .WithSummary("Lista registros de auditoria (paginado)")
            .WithDescription("""
                Consulta a trilha de auditoria, do mais recente para o mais antigo (`occurredOn` desc). Filtros opcionais, combinados com AND:

                | Parâmetro | Descrição |
                |-----------|-----------|
                | `module` | Módulo de origem (ex.: `Venues`, `People`) |
                | `entityName` | Nome da entidade (ex.: `Venue`, `Room`, `Person`) |
                | `entityId` | Identificador da entidade (texto; normalmente um Guid) |
                | `userId` | Guid do usuário que fez a alteração |
                | `operation` | `Insert`, `Update` ou `Delete` |
                | `occurredFrom`, `occurredUntil` | Intervalo (ISO-8601, inclusivo) do momento da alteração |
                | `page`, `pageSize` | Paginação (máx. 100 por página) |
                | `sortBy`, `direction` | Campo permitido e direção `Asc`/`Desc` |

                A listagem não traz os dados antes/depois; use `GET /api/v1/audit/records/{id}` para o detalhe.
                **Perfil exigido:** Administrador.
                """)
            .WithValidation<ListAuditRecordsRequest>()
            .RequireAuthorization(Policies.Administration)
            .Produces<PagedResult<ListAuditRecordsItemResponse>>();
}
