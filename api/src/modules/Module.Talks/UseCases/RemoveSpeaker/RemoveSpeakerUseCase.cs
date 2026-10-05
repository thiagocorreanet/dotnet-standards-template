using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.RemoveSpeaker;

[Command("event-management-example")]
internal sealed class RemoveSpeakerUseCase(TalksDbContext db) : IUseCase<RemoveSpeakerRequest, RemoveSpeakerResponse>
{
    public async Task<Result<RemoveSpeakerResponse>> HandleAsync(RemoveSpeakerRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.RemoveSpeaker.Load")
            .Include(p => p.Speakers)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            return TalksErrors.TalkNotFound;
        }

        var result = talk.RemoveSpeaker(request.PersonId);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.TalkSpeakers.Remove(result.Value);
            await db.SaveChangesAsync(ct);
            return Result.Success(new RemoveSpeakerResponse(talk.Id, request.PersonId));
        }, cancellationToken);
    }
}
