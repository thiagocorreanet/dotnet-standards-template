using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.AddContent;

[Command("event-management-example")]
internal sealed class AddContentUseCase(TalksDbContext db, ILogger<AddContentUseCase> logger) : IUseCase<AddContentRequest, AddContentResponse>
{
    public async Task<Result<AddContentResponse>> HandleAsync(AddContentRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.AddContent.Load")
            .Include(p => p.Contents)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            logger.LogInformation("Adição de conteúdo rejeitada: palestra {TalkId} não encontrada", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        logger.LogDebug("Chamando agregado Palestra {TalkId} para adicionar conteúdo do tipo {ContentType}; conteúdos atuais={ContentCount}", talk.Id, request.ContentType, talk.Contents.Count);
        var content = talk.AddContent(request.ContentTitle, request.ContentType, request.ContentUrl, request.ContentDescription);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Conteúdo {ContentId} adicionado à palestra {TalkId}; total de conteúdos={ContentCount}", content.Id, talk.Id, talk.Contents.Count);
            return Result.Success(new AddContentResponse(content.Id, content.TalkId, content.ContentTitle, content.ContentType, content.ContentUrl));
        }, cancellationToken);
    }
}
