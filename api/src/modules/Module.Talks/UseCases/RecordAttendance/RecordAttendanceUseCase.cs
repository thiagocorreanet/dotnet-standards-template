using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.Events;
using Shared.Contracts.Talks;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.RecordAttendance;

/// <summary>Registra presença de um participante com inscrição confirmada no evento. Emite <see cref="AttendanceRecorded"/>.</summary>
[Command("event-management-example")]
internal sealed class RecordAttendanceUseCase(TalksDbContext db, IEventsModuleApi eventsApi, TimeProvider timeProvider, ILogger<RecordAttendanceUseCase> logger) : IUseCase<RecordAttendanceRequest, RecordAttendanceResponse>
{
    public async Task<Result<RecordAttendanceResponse>> HandleAsync(RecordAttendanceRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.RecordAttendance.Load")
            .Include(p => p.Attendances.Where(x => x.PersonId == request.PersonId))
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            logger.LogInformation("Registro de presença rejeitado: palestra {TalkId} não encontrada", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        logger.LogDebug("Consultando módulo Eventos para validar inscrição da pessoa {PersonId} no evento {EventId}", request.PersonId, talk.EventId);
        var registered = await eventsApi.ConfirmedRegistrationExistsAsync(talk.EventId, request.PersonId, cancellationToken);
        if (!registered)
        {
            logger.LogInformation("Presença rejeitada: pessoa {PersonId} não possui inscrição confirmada no evento {EventId}", request.PersonId, talk.EventId);
            return TalksErrors.ParticipantNotRegistered;
        }

        logger.LogDebug("Chamando agregado Palestra {TalkId}.RegistrarPresenca para pessoa {PersonId}; presenças carregadas={AttendanceCount}", talk.Id, request.PersonId, talk.Attendances.Count);
        var result = talk.RecordAttendance(request.PersonId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            logger.LogInformation("Presença da pessoa {PersonId} na palestra {TalkId} rejeitada pela regra {ErrorCode}", request.PersonId, talk.Id, result.Error.Code);
            return result.Error;
        }

        var attendance = result.Value;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Presença {AttendanceId} registrada para pessoa {PersonId} na palestra {TalkId}", attendance.Id, attendance.PersonId, attendance.TalkId);
            return Result.Success(new RecordAttendanceResponse(attendance.Id, attendance.TalkId, attendance.PersonId, attendance.AttendanceRecordedAt));
        }, cancellationToken);
    }
}
