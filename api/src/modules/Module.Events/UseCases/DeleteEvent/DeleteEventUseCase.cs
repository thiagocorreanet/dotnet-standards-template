using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Events.UseCases.DeleteEvent;

/// <summary>Exclusão lógica do evento e de suas inscrições (interceptor converte Remove em soft delete).</summary>
[Command("event-management-example")]
internal sealed class DeleteEventUseCase(EventsDbContext db) : IUseCase<DeleteEventRequest, DeleteEventResponse>
{
    public async Task<Result<DeleteEventResponse>> HandleAsync(DeleteEventRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.DeleteEvent.Load")
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (eventEntity is null)
        {
            return EventsErrors.EventNotFound;
        }

        var result = eventEntity.MarkDeleted();
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Registrations.RemoveRange(eventEntity.Registrations);
            db.Events.Remove(eventEntity);
            await db.SaveChangesAsync(ct);
            return Result.Success(new DeleteEventResponse(eventEntity.Id));
        }, cancellationToken);
    }
}
