using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.Events;
using Shared.Contracts.Venues;
using Shared.Contracts.People;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.GetTalk;

/// <summary>Projeção da palestra enriquecida com nomes de evento, sala e pessoas obtidos via contratos (uma chamada em lote para pessoas).</summary>
internal sealed class GetTalkUseCase(
    TalksDbContext db,
    IEventsModuleApi eventsApi,
    IVenuesModuleApi venuesApi,
    IPeopleModuleApi peopleApi) : IUseCase<GetTalkRequest, GetTalkResponse>
{
    public async Task<Result<GetTalkResponse>> HandleAsync(GetTalkRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.GetTalk")
            .AsNoTracking()
            .Where(p => p.Id == request.TalkId)
            .Select(p => new
            {
                p.Id,
                p.EventId,
                p.TrackId,
                p.RoomId,
                p.TalkTitle,
                p.TalkDescription,
                p.TalkStart,
                p.TalkEnd,
                Speakers = p.Speakers.OrderBy(x => x.CreatedAt).Select(x => new { x.PersonId, x.SpeakerRole }).ToList(),
                Contents = p.Contents.OrderBy(c => c.CreatedAt)
                    .Select(c => new GetTalkContentResponse(c.Id, c.ContentTitle, c.ContentType, c.ContentUrl, c.ContentDescription))
                    .ToList(),
                AttendancesCount = p.Attendances.Count,
                CertificatesCount = p.Certificates.Count,
                p.CreatedAt,
                p.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (talk is null)
        {
            return TalksErrors.TalkNotFound;
        }

        var eventEntity = await eventsApi.GetEventSummaryAsync(talk.EventId, cancellationToken);
        var track = await eventsApi.GetTrackSummaryAsync(talk.EventId, talk.TrackId, cancellationToken);
        RoomSummary? room;
        if (talk.RoomId.HasValue)
        {
            room = await venuesApi.GetRoomSummaryAsync(talk.RoomId.Value, cancellationToken);
        }
        else
        {
            room = null;
        }
        var personIds = talk.Speakers.Select(x => x.PersonId).Distinct().ToList();
        var people = await peopleApi.GetPeopleSummaryAsync(personIds, cancellationToken);
        var names = people.ToDictionary(p => p.Id, p => p.PersonName);

        return new GetTalkResponse(
            talk.Id,
            talk.EventId,
            eventEntity?.EventName ?? string.Empty,
            talk.TrackId,
            track?.TrackName ?? string.Empty,
            talk.RoomId,
            room?.RoomName,
            talk.TalkTitle,
            talk.TalkDescription,
            talk.TalkStart,
            talk.TalkEnd,
            (int)(talk.TalkEnd - talk.TalkStart).TotalMinutes,
            talk.Speakers.Select(x => new GetTalkSpeakerResponse(x.PersonId, names.GetValueOrDefault(x.PersonId, string.Empty), x.SpeakerRole)).ToList(),
            talk.Contents,
            talk.AttendancesCount,
            talk.CertificatesCount,
            talk.CreatedAt,
            talk.UpdatedAt);
    }
}
