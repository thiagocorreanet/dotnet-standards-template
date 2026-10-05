using Microsoft.EntityFrameworkCore;
using Module.Audit.Domain;
using Module.Audit.Shared;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Audit.UseCases.GetAuditRecord;

internal sealed class GetAuditRecordUseCase(AuditDbContext db) : IUseCase<GetAuditRecordRequest, GetAuditRecordResponse>
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
            return AuditErrors.RecordNotFound;
        }

        return record;
    }
}
