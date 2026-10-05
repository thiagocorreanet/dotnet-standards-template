using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.Talks;
using Shared.Contracts.People;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.CreateTalk;

/// <summary>Cria a palestra validando evento, período, sala (local e agenda) e palestrantes via contratos. Emite <see cref="TalkCreated"/>.</summary>
[Command("event-management-example")]
internal sealed class CreateTalkUseCase(
    TalksDbContext db,
    TalkScheduleChecker schedule,
    IPeopleModuleApi peopleApi) : IUseCase<CreateTalkRequest, CreateTalkResponse>
{
    public async Task<Result<CreateTalkResponse>> HandleAsync(CreateTalkRequest request, CancellationToken cancellationToken)
    {
        var verification = await schedule.CheckAsync(request.EventId, request.TrackId, request.RoomId, request.TalkStart, request.TalkEnd, cancellationToken);
        if (verification.IsFailure)
        {
            return verification.Error;
        }

        if (request.RoomId.HasValue)
        {
            var occupiedRoom = await db.Talks
                .TagWith("Talks.CreateTalk.CheckRoom")
                .AnyAsync(TalkSchedule.OccupiesRoom(request.RoomId.Value, request.TalkStart, request.TalkEnd), cancellationToken);
            if (occupiedRoom)
            {
                return TalksErrors.RoomOccupied;
            }
        }

        var personIds = request.Speakers.Select(p => p.PersonId).Distinct().ToList();
        var people = await peopleApi.GetPeopleSummaryAsync(personIds, cancellationToken);
        var missingSpeakers = personIds.Count(id => people.All(p => p.Id != id));
        if (missingSpeakers > 0)
        {
            return TalksErrors.PersonNotFound;
        }

        var result = Talk.Create(
            request.EventId, request.TrackId, request.RoomId, request.TalkTitle, request.TalkDescription, request.TalkStart, request.TalkEnd,
            request.Speakers.Select(p => new NewSpeaker(p.PersonId, p.SpeakerRole)).ToList());
        if (result.IsFailure)
        {
            return result.Error;
        }

        var talk = result.Value;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Talks.Add(talk);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CreateTalkResponse(talk.Id, talk.EventId, talk.TalkTitle));
        }, cancellationToken);
    }
}
