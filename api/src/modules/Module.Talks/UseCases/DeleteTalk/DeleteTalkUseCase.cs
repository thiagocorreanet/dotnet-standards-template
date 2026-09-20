using Shared.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.DeleteTalk;

/// <summary>Exclusão lógica da palestra, de seus palestrantes e conteúdos (interceptor converte Remove em soft delete). Presenças e certificados permanecem.</summary>
[Command("event-management-example")]
internal sealed class DeleteTalkUseCase(TalksDbContext db, IEventsModuleApi events, ILogger<DeleteTalkUseCase> logger) : IUseCase<DeleteTalkRequest, DeleteTalkResponse>
{
    public async Task<Result<DeleteTalkResponse>> HandleAsync(DeleteTalkRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.DeleteTalk.Load")
            .Include(p => p.Speakers)
            .Include(p => p.Contents)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            logger.LogInformation("Palestra {TalkId} não encontrada para exclusão", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        var eventEntity = await events.GetEventSummaryAsync(talk.EventId, cancellationToken);
        if (eventEntity?.EventStatus is "Published" or "InProgress" &&
            await db.Talks.CountAsync(p => p.EventId == talk.EventId && p.IsActive, cancellationToken) <= 1)
            return Error.Conflict("Talks.LastTalk", "Um evento publicado deve manter ao menos uma palestra ativa.");

        logger.LogDebug("Preparando exclusão lógica da palestra {TalkId}: palestrantes={SpeakerCount}, conteúdos={ContentCount}; presenças e certificados serão preservados", talk.Id, talk.Speakers.Count, talk.Contents.Count);
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.TalkSpeakers.RemoveRange(talk.Speakers);
            db.TalkContents.RemoveRange(talk.Contents);
            db.Talks.Remove(talk);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Palestra {TalkId}, {SpeakerCount} palestrante(s) e {ContentCount} conteúdo(s) excluídos logicamente", talk.Id, talk.Speakers.Count, talk.Contents.Count);
            return Result.Success(new DeleteTalkResponse(talk.Id));
        }, cancellationToken);
    }
}
