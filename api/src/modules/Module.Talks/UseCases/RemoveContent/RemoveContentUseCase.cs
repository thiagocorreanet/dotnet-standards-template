using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.RemoveContent;

[Command("event-management-example")]
internal sealed class RemoveContentUseCase(TalksDbContext db) : IUseCase<RemoveContentRequest, RemoveContentResponse>
{
    public async Task<Result<RemoveContentResponse>> HandleAsync(RemoveContentRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.RemoveContent.Load")
            .Include(p => p.Contents)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            return TalksErrors.TalkNotFound;
        }

        var result = talk.RemoveContent(request.ContentId);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.TalkContents.Remove(result.Value);
            await db.SaveChangesAsync(ct);
            return Result.Success(new RemoveContentResponse(result.Value.Id));
        }, cancellationToken);
    }
}
