using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.DeleteEvent;

/// <summary>Exclusão lógica do evento e de suas inscrições (interceptor converte Remove em soft delete).</summary>
[Command("event-management-example")]
internal sealed class DeleteEventUseCase(EventsDbContext db, ILogger<DeleteEventUseCase> logger) : IUseCase<DeleteEventRequest, DeleteEventResponse>
{
    public async Task<Result<DeleteEventResponse>> HandleAsync(DeleteEventRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.DeleteEvent.Load")
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (eventEntity is null)
        {
            logger.LogInformation("Evento {EventId} não encontrado para exclusão", request.EventId);
            return EventsErrors.EventNotFound;
        }

        logger.LogDebug("Chamando agregado Evento {EventId} para marcar exclusão; situação={EventStatus}, inscrições={RegistrationCount}", eventEntity.Id, eventEntity.EventStatus, eventEntity.Registrations.Count);
        var result = eventEntity.MarkDeleted();
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Evento {EventId} rejeitou exclusão pela regra {ErrorCode}", eventEntity.Id, result.Error.Code);
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Registrations.RemoveRange(eventEntity.Registrations);
            db.Events.Remove(eventEntity);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Evento {EventId} e {RegistrationCount} inscrição(ões) excluídos logicamente", eventEntity.Id, eventEntity.Registrations.Count);
            return Result.Success(new DeleteEventResponse(eventEntity.Id));
        }, cancellationToken);
    }
}
