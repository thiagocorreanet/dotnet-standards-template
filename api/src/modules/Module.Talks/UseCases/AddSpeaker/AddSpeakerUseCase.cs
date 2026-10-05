using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.People;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.AddSpeaker;

[Command("event-management-example")]
internal sealed class AddSpeakerUseCase(TalksDbContext db, IPeopleModuleApi peopleApi) : IUseCase<AddSpeakerRequest, AddSpeakerResponse>
{
    public async Task<Result<AddSpeakerResponse>> HandleAsync(AddSpeakerRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.AddSpeaker.Load")
            .Include(p => p.Speakers)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            return TalksErrors.TalkNotFound;
        }

        var person = await peopleApi.GetPersonSummaryAsync(request.PersonId, cancellationToken);
        if (person is null)
        {
            return TalksErrors.PersonNotFound;
        }

        var result = talk.AddSpeaker(request.PersonId, request.SpeakerRole);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var speaker = result.Value;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new AddSpeakerResponse(speaker.TalkId, speaker.PersonId, speaker.SpeakerRole));
        }, cancellationToken);
    }
}
