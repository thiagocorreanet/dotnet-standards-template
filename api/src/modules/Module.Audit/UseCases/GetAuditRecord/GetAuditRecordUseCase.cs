using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Audit.Domain;
using Module.Audit.Shared;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Audit.UseCases.GetAuditRecord;

internal sealed class GetAuditRecordUseCase(AuditDbContext db, ILogger<GetAuditRecordUseCase> logger) : IUseCase<GetAuditRecordRequest, GetAuditRecordResponse>
{
    public async Task<Result<GetAuditRecordResponse>> HandleAsync(GetAuditRecordRequest request, CancellationToken cancellationToken)
    {
        var record = await db.AuditRecords
            .TagWith("Audit.GetAuditRecord")
            .AsNoTracking()
            .Where(r => r.Id == request.RecordId)
            .Select(r => new GetAuditRecordResponse(
                r.Id, r.Module, r.EntityName, r.EntityId, r.Operation, r.PreviousData, r.NewData,
                r.UserId, r.UserName, r.TraceId, r.OccurredOn, r.RecordedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            logger.LogInformation("Registro de auditoria {AuditRecordId} não encontrado", request.RecordId);
            return AuditErrors.RecordNotFound;
        }

        logger.LogInformation("Registro de auditoria {AuditRecordId} carregado para {Module}.{EntityType}", record.Id, record.Module, record.EntityName);
        return record;
    }
}
