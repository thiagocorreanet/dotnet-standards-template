using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.RemoveContent;

[Command("event-management-example")]
internal sealed class RemoveContentUseCase(TalksDbContext db, ILogger<RemoveContentUseCase> logger) : IUseCase<RemoveContentRequest, RemoveContentResponse>
{
    public async Task<Result<RemoveContentResponse>> HandleAsync(RemoveContentRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.RemoveContent.Load")
            .Include(p => p.Contents)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            logger.LogInformation("Remoção de conteúdo rejeitada: palestra {TalkId} não encontrada", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        logger.LogDebug("Chamando agregado Palestra {TalkId} para remover conteúdo {ContentId}", talk.Id, request.ContentId);
        var result = talk.RemoveContent(request.ContentId);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Palestra {TalkId} rejeitou remoção do conteúdo {ContentId} pela regra {ErrorCode}", talk.Id, request.ContentId, result.Error.Code);
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.TalkContents.Remove(result.Value);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Conteúdo {ContentId} removido da palestra {TalkId}; conteúdos restantes={ContentCount}", result.Value.Id, talk.Id, talk.Contents.Count);
            return Result.Success(new RemoveContentResponse(result.Value.Id));
        }, cancellationToken);
    }
}
