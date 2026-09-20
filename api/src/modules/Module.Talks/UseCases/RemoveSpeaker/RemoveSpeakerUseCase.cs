using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.RemoveSpeaker;

[Command("event-management-example")]
internal sealed class RemoveSpeakerUseCase(TalksDbContext db, ILogger<RemoveSpeakerUseCase> logger) : IUseCase<RemoveSpeakerRequest, RemoveSpeakerResponse>
{
    public async Task<Result<RemoveSpeakerResponse>> HandleAsync(RemoveSpeakerRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.RemoveSpeaker.Load")
            .Include(p => p.Speakers)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            logger.LogInformation("Remoção de palestrante rejeitada: palestra {TalkId} não encontrada", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        logger.LogDebug("Chamando agregado Palestra {TalkId} para remover pessoa {PersonId}; palestrantes atuais={SpeakerCount}", talk.Id, request.PersonId, talk.Speakers.Count);
        var result = talk.RemoveSpeaker(request.PersonId);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Palestra {TalkId} rejeitou remoção da pessoa {PersonId} pela regra {ErrorCode}", talk.Id, request.PersonId, result.Error.Code);
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.TalkSpeakers.Remove(result.Value);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pessoa {PersonId} removida da palestra {TalkId}; palestrantes restantes={SpeakerCount}", request.PersonId, talk.Id, talk.Speakers.Count);
            return Result.Success(new RemoveSpeakerResponse(talk.Id, request.PersonId));
        }, cancellationToken);
    }
}
