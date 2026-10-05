using Shared.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.DeleteTalk;

/// <summary>Exclusão lógica da palestra, de seus palestrantes e conteúdos (interceptor converte Remove em soft delete). Presenças e certificados permanecem.</summary>
[Command("event-management-example")]
internal sealed class DeleteTalkUseCase(TalksDbContext db, IEventsModuleApi events) : IUseCase<DeleteTalkRequest, DeleteTalkResponse>
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
            return TalksErrors.TalkNotFound;
        }

        var eventEntity = await events.GetEventSummaryAsync(talk.EventId, cancellationToken);
        if (eventEntity?.EventStatus is "Published" or "InProgress" &&
            await db.Talks.CountAsync(p => p.EventId == talk.EventId && p.IsActive, cancellationToken) <= 1)
            return Error.Conflict("Talks.LastTalk", "Um evento publicado deve manter ao menos uma palestra ativa.");

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.TalkSpeakers.RemoveRange(talk.Speakers);
            db.TalkContents.RemoveRange(talk.Contents);
            db.Talks.Remove(talk);
            await db.SaveChangesAsync(ct);
            return Result.Success(new DeleteTalkResponse(talk.Id));
        }, cancellationToken);
    }
}
