using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.CancelRegistration;

/// <summary>Cancelamento não é soft delete: a inscrição permanece com situação <c>Canceled</c> e data de cancelamento.</summary>
[Command("event-management-example")]
internal sealed class CancelRegistrationUseCase(EventsDbContext db, TimeProvider timeProvider, ILogger<CancelRegistrationUseCase> logger) : IUseCase<CancelRegistrationRequest, CancelRegistrationResponse>
{
    public async Task<Result<CancelRegistrationResponse>> HandleAsync(CancelRegistrationRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.CancelRegistration.Load")
            .Include(e => e.Registrations.Where(i => i.Id == request.RegistrationId))
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (eventEntity is null)
        {
            logger.LogInformation("Cancelamento de inscrição rejeitado: evento {EventId} não encontrado", request.EventId);
            return EventsErrors.EventNotFound;
        }

        logger.LogDebug("Chamando agregado Evento {EventId} para cancelar inscrição {RegistrationId}", eventEntity.Id, request.RegistrationId);
        var result = eventEntity.CancelRegistration(request.RegistrationId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Evento {EventId} rejeitou cancelamento da inscrição {RegistrationId} pela regra {ErrorCode}", eventEntity.Id, request.RegistrationId, result.Error.Code);
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Inscrição {RegistrationId} do evento {EventId} cancelada", result.Value.Id, eventEntity.Id);
            return Result.Success(new CancelRegistrationResponse(result.Value.Id));
        }, cancellationToken);
    }
}
