using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Audit;
using Shared.Contracts.Integration;
namespace Module.Audit.Shared.Handlers;
/// <summary>PK do evento + ON CONFLICT torna a entrega concorrente atomicamente idempotente.</summary>
internal sealed class EntityChangedHandler(AuditDbContext db, TimeProvider time)
    : IIntegrationEventHandler<EntityChanged>
{
    public async Task HandleAsync(EntityChanged e, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Audit"."AuditRecords"
            ("Id","Module","EntityName","EntityId","Operation","PreviousData","NewData",
             "UserId","UserName","TraceId","OccurredOn","RecordedAt")
            VALUES ({e.Id},{e.Module},{e.EntityName},{e.EntityId},{e.Operation},
                CAST({e.PreviousData} AS jsonb),CAST({e.NewData} AS jsonb),
                {e.UserId},{e.UserName},{e.TraceId},{e.OccurredOn},{time.GetUtcNow()})
            ON CONFLICT ("Id") DO NOTHING
            """, ct);
    }
}
