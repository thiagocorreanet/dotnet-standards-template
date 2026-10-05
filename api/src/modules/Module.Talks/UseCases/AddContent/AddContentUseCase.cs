using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.AddContent;

[Command("event-management-example")]
internal sealed class AddContentUseCase(TalksDbContext db) : IUseCase<AddContentRequest, AddContentResponse>
{
    public async Task<Result<AddContentResponse>> HandleAsync(AddContentRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.AddContent.Load")
            .Include(p => p.Contents)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            return TalksErrors.TalkNotFound;
        }

        var content = talk.AddContent(request.ContentTitle, request.ContentType, request.ContentUrl, request.ContentDescription);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new AddContentResponse(content.Id, content.TalkId, content.ContentTitle, content.ContentType, content.ContentUrl));
        }, cancellationToken);
    }
}
