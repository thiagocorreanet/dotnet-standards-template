using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.Events;
using Shared.Contracts.Talks;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.RecordAttendance;

/// <summary>Registra presença de um participante com inscrição confirmada no evento. Emite <see cref="AttendanceRecorded"/>.</summary>
[Command("event-management-example")]
internal sealed class RecordAttendanceUseCase(TalksDbContext db, IEventsModuleApi eventsApi, TimeProvider timeProvider) : IUseCase<RecordAttendanceRequest, RecordAttendanceResponse>
{
    public async Task<Result<RecordAttendanceResponse>> HandleAsync(RecordAttendanceRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.RecordAttendance.Load")
            .Include(p => p.Attendances.Where(x => x.PersonId == request.PersonId))
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            return TalksErrors.TalkNotFound;
        }

        var registered = await eventsApi.ConfirmedRegistrationExistsAsync(talk.EventId, request.PersonId, cancellationToken);
        if (!registered)
        {
            return TalksErrors.ParticipantNotRegistered;
        }

        var result = talk.RecordAttendance(request.PersonId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error;
        }

        var attendance = result.Value;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new RecordAttendanceResponse(attendance.Id, attendance.TalkId, attendance.PersonId, attendance.AttendanceRecordedAt));
        }, cancellationToken);
    }
}
