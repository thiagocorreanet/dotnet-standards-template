using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.UpdateTalk;

/// <summary>Atualiza dados e agenda da palestra, revalidando evento, período e sala (ignorando a própria palestra na sobreposição).</summary>
[Command("event-management-example")]
internal sealed class UpdateTalkUseCase(TalksDbContext db, TalkScheduleChecker schedule, ILogger<UpdateTalkUseCase> logger) : IUseCase<UpdateTalkRequest, UpdateTalkResponse>
{
    public async Task<Result<UpdateTalkResponse>> HandleAsync(UpdateTalkRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.UpdateTalk.Load")
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            logger.LogInformation("Palestra {TalkId} não encontrada para atualização", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        logger.LogDebug("Revalidando agenda da palestra {TalkId} no evento {EventId}, trilha {TrackId} e sala {RoomId}", talk.Id, talk.EventId, request.TrackId, request.RoomId);
        var verification = await schedule.CheckAsync(talk.EventId, request.TrackId, request.RoomId, request.TalkStart, request.TalkEnd, cancellationToken);
        if (verification.IsFailure)
        {
            logger.LogInformation("Atualização da palestra {TalkId} rejeitada pela regra de agenda {ErrorCode}", talk.Id, verification.Error.Code);
            return verification.Error;
        }

        if (request.RoomId.HasValue)
        {
            logger.LogDebug("Verificando sobreposição da sala {RoomId} para palestra {TalkId}", request.RoomId, talk.Id);
            var occupiedRoom = await db.Talks
                .TagWith("Talks.UpdateTalk.CheckRoom")
                .AnyAsync(TalkSchedule.OccupiesRoom(request.RoomId.Value, request.TalkStart, request.TalkEnd, talk.Id), cancellationToken);
            if (occupiedRoom)
            {
                logger.LogInformation("Atualização da palestra {TalkId} rejeitada porque a sala {RoomId} está ocupada", talk.Id, request.RoomId);
                return TalksErrors.RoomOccupied;
            }
            logger.LogDebug("Sala {RoomId} disponível no período solicitado para palestra {TalkId}", request.RoomId, talk.Id);
        }
        else
        {
            logger.LogDebug("Palestra {TalkId} será atualizada sem sala; verificação de sobreposição ignorada", talk.Id);
        }

        logger.LogDebug("Chamando agregado Palestra {TalkId}.Atualizar", talk.Id);
        talk.Update(request.TrackId, request.RoomId, request.TalkTitle, request.TalkDescription, request.TalkStart, request.TalkEnd);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Palestra {TalkId} atualizada na trilha {TrackId} e sala {RoomId}", talk.Id, talk.TrackId, talk.RoomId);
            return Result.Success(new UpdateTalkResponse(talk.Id, talk.TalkTitle, talk.UpdatedAt));
        }, cancellationToken);
    }
}
