using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.UpdateTalk;

/// <summary>Atualiza dados e agenda da palestra, revalidando evento, período e sala (ignorando a própria palestra na sobreposição).</summary>
[Command("event-management-example")]
internal sealed class UpdateTalkUseCase(TalksDbContext db, TalkScheduleChecker schedule) : IUseCase<UpdateTalkRequest, UpdateTalkResponse>
{
    public async Task<Result<UpdateTalkResponse>> HandleAsync(UpdateTalkRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.UpdateTalk.Load")
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            return TalksErrors.TalkNotFound;
        }

        var verification = await schedule.CheckAsync(talk.EventId, request.TrackId, request.RoomId, request.TalkStart, request.TalkEnd, cancellationToken);
        if (verification.IsFailure)
        {
            return verification.Error;
        }

        if (request.RoomId.HasValue)
        {
            var occupiedRoom = await db.Talks
                .TagWith("Talks.UpdateTalk.CheckRoom")
                .AnyAsync(TalkSchedule.OccupiesRoom(request.RoomId.Value, request.TalkStart, request.TalkEnd, talk.Id), cancellationToken);
            if (occupiedRoom)
            {
                return TalksErrors.RoomOccupied;
            }
        }

        talk.Update(request.TrackId, request.RoomId, request.TalkTitle, request.TalkDescription, request.TalkStart, request.TalkEnd);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new UpdateTalkResponse(talk.Id, talk.TalkTitle, talk.UpdatedAt));
        }, cancellationToken);
    }
}
