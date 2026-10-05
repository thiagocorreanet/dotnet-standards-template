using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Audit;
using Shared.Contracts.Integration;
using Shared.Messaging;
namespace Module.Audit.Shared.Handlers;
/// <summary>PK do evento + ON CONFLICT torna a entrega concorrente atomicamente idempotente.</summary>
/// <remarks>
/// Sem Inbox de propósito: é o consumidor de maior volume (toda alteração auditada), a idempotência já está no destino
/// e a tabela é append-only (runtime só com SELECT/INSERT). A Inbox dobraria as gravações sem proteger nada a mais.
/// </remarks>
[SkipInbox("Idempotente no destino: PK do evento com ON CONFLICT DO NOTHING em tabela append-only.")]
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
