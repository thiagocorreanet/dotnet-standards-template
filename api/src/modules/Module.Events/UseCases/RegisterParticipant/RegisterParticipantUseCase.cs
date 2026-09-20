using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Npgsql;
using Shared.Contracts.Venues;
using Shared.Contracts.People;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.RegisterParticipant;

/// <summary>
/// Inscreve uma pessoa em um evento publicado/em andamento respeitando a capacidade. A contagem de confirmadas é uma consulta
/// projetada (não carrega a coleção); a corrida entre requisições concorrentes é resolvida pelo índice único filtrado do banco.
/// </summary>
[Command("event-management-example")]
internal sealed class RegisterParticipantUseCase(
    EventsDbContext db,
    IPeopleModuleApi people,
    IVenuesModuleApi venues,
    TimeProvider timeProvider,
    ILogger<RegisterParticipantUseCase> logger) : IUseCase<RegisterParticipantRequest, RegisterParticipantResponse>
{
    public async Task<Result<RegisterParticipantResponse>> HandleAsync(RegisterParticipantRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.RegisterParticipant.LoadEvent")
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (eventEntity is null)
        {
            logger.LogInformation("Inscrição rejeitada: evento {EventId} não encontrado", request.EventId);
            return EventsErrors.EventNotFound;
        }

        if (!eventEntity.AcceptsRegistrations)
        {
            logger.LogInformation("Inscrição rejeitada: evento {EventId} em situação {EventStatus} não aceita inscrições", eventEntity.Id, eventEntity.EventStatus);
            return EventsErrors.EventDoesNotAcceptRegistrations;
        }

        logger.LogDebug("Consultando módulo Pessoas para validar participante {PersonId}", request.PersonId);
        var person = await people.GetPersonSummaryAsync(request.PersonId, cancellationToken);
        if (person is null)
        {
            logger.LogInformation("Inscrição no evento {EventId} rejeitada: pessoa {PersonId} não encontrada", eventEntity.Id, request.PersonId);
            return EventsErrors.PersonNotFound;
        }

        var alreadyRegistered = await db.Registrations
            .TagWith("Events.RegisterParticipant.CheckRegistrationConfirmed")
            .AnyAsync(i => i.EventId == eventEntity.Id && i.PersonId == request.PersonId && i.RegistrationStatus == RegistrationStatus.Confirmed, cancellationToken);
        if (alreadyRegistered)
        {
            logger.LogInformation("Inscrição duplicada rejeitada para pessoa {PersonId} no evento {EventId}", request.PersonId, eventEntity.Id);
            return EventsErrors.PersonAlreadyRegistered;
        }

        var confirmed = await db.Registrations
            .TagWith("Events.RegisterParticipant.CountConfirmed")
            .CountAsync(i => i.EventId == eventEntity.Id && i.RegistrationStatus == RegistrationStatus.Confirmed, cancellationToken);
        logger.LogInformation("Evento {EventId} possui {ConfirmedRegistrations} inscrição(ões) confirmada(s) antes da nova inscrição", eventEntity.Id, confirmed);

        int? venueTotalCapacity = null;
        if (eventEntity.EventMaximumCapacity is null && eventEntity.VenueId.HasValue)
        {
            logger.LogDebug("Consultando módulo Locais para obter capacidade do local {VenueId}", eventEntity.VenueId);
            var venue = await venues.GetVenueSummaryAsync(eventEntity.VenueId.Value, cancellationToken);
            if (venue is null) return EventsErrors.VenueNotFound;
            venueTotalCapacity = venue.VenueTotalCapacity;
            logger.LogInformation("Capacidade considerada para o evento {EventId}: {CapacitySource}={Capacity}", eventEntity.Id, "Venue", venueTotalCapacity);
        }
        else
        {
            logger.LogInformation("Capacidade considerada para o evento {EventId}: {CapacitySource}={Capacity}", eventEntity.Id, "Event", eventEntity.EventMaximumCapacity);
        }

        logger.LogDebug("Chamando agregado Evento {EventId}.Inscrever para pessoa {PersonId}, confirmadas={ConfirmedRegistrations}, capacidade={Capacity}", eventEntity.Id, request.PersonId, confirmed, venueTotalCapacity ?? eventEntity.EventMaximumCapacity);
        var result = eventEntity.Register(request.PersonId, confirmed, venueTotalCapacity, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            logger.LogInformation("Inscrição da pessoa {PersonId} no evento {EventId} rejeitada pela regra {ErrorCode}", request.PersonId, eventEntity.Id, result.Error.Code);
            return result.Error;
        }

        var registration = result.Value;
        try
        {
            return await db.ExecuteInTransactionAsync(async ct =>
            {
                db.Registrations.Add(registration);
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Inscrição {RegistrationId} confirmada para pessoa {PersonId} no evento {EventId}", registration.Id, registration.PersonId, registration.EventId);
                return Result.Success(new RegisterParticipantResponse(registration.Id, registration.EventId, registration.PersonId, registration.RegistrationStatus, registration.RegistrationRegisteredAt));
            }, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Duas requisições passaram pela verificação ao mesmo tempo; o índice único filtrado garantiu uma só inscrição confirmada.
            logger.LogWarning("Concorrência detectada ao inscrever pessoa {PersonId} no evento {EventId}; índice único preservou a regra de inscrição única", request.PersonId, eventEntity.Id);
            return EventsErrors.PersonAlreadyRegistered;
        }
    }
}
