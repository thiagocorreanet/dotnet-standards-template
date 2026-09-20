using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Venues;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.UpdateEvent;

[Command("event-management-example")]
internal sealed class UpdateEventUseCase(EventsDbContext db, ITalksModuleApi talks, IVenuesModuleApi venues, ILogger<UpdateEventUseCase> logger) : IUseCase<UpdateEventRequest, UpdateEventResponse>
{
    public async Task<Result<UpdateEventResponse>> HandleAsync(UpdateEventRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.UpdateEvent.Load")
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (eventEntity is null)
        {
            logger.LogInformation("Evento {EventId} não encontrado para atualização", request.EventId);
            return EventsErrors.EventNotFound;
        }

        if (!eventEntity.CanBeUpdated)
        {
            logger.LogInformation("Atualização do evento {EventId} rejeitada porque a situação {EventStatus} não permite alteração", eventEntity.Id, eventEntity.EventStatus);
            return EventsErrors.EventCannotBeUpdated;
        }

        if ((eventEntity.VenueId != request.VenueId || eventEntity.EventStartDate != request.EventStartDate ||
                eventEntity.EventEndDate != request.EventEndDate || eventEntity.EventFormat != request.EventFormat) &&
            await talks.CountEventTalksAsync(eventEntity.Id, cancellationToken) > 0)
            return Error.Conflict("Events.ScheduleExisting", "Remova ou ajuste as palestras antes de alterar local, formato ou período.");
        var confirmed = await db.Registrations.CountAsync(i => i.EventId == eventEntity.Id && i.RegistrationStatus == RegistrationStatus.Confirmed, cancellationToken);
        if (request.EventMaximumCapacity.HasValue && request.EventMaximumCapacity < confirmed)
            return Error.Conflict("Events.CapacityInsufficient", "A capacidade não pode ser menor que as inscrições confirmadas.");

        logger.LogDebug("Atualização do evento {EventId} referencia local={HasVenue}", eventEntity.Id, request.VenueId.HasValue);
        if (request.VenueId.HasValue)
        {
            logger.LogDebug("Consultando módulo Locais para validar local {VenueId} do evento {EventId}", request.VenueId, eventEntity.Id);
            var venue = await venues.GetVenueSummaryAsync(request.VenueId.Value, cancellationToken);
            if (venue is null)
            {
                logger.LogInformation("Atualização do evento {EventId} rejeitada: local {VenueId} não encontrado", eventEntity.Id, request.VenueId);
                return EventsErrors.VenueNotFound;
            }
            if (request.EventMaximumCapacity is null && venue.VenueTotalCapacity < confirmed)
                return Error.Conflict("Events.CapacityInsufficient", "A capacidade do local não pode ser menor que as inscrições confirmadas.");
        }

        logger.LogDebug("Chamando agregado Evento {EventId} para atualizar dados e formato {EventFormat}", eventEntity.Id, request.EventFormat);
        var result = eventEntity.Update(
            request.EventName, request.EventDescription, request.EventStartDate, request.EventEndDate,
            request.EventFormat, request.VenueId, request.EventRemoteUrl, request.EventMaximumCapacity);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Evento {EventId} rejeitou atualização pela regra {ErrorCode}", eventEntity.Id, result.Error.Code);
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Evento {EventId} atualizado em situação {EventStatus}", eventEntity.Id, eventEntity.EventStatus);
            return Result.Success(new UpdateEventResponse(eventEntity.Id, eventEntity.EventName, eventEntity.EventStatus, eventEntity.UpdatedAt));
        }, cancellationToken);
    }
}
